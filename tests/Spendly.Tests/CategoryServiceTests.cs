using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Common;
using Spendly.Web.Data.Seed;
using Spendly.Web.Domain;
using Spendly.Web.Models.Categories;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class CategoryServiceTests
{
    private sealed record Setup(TestDb Db, CategoryService Service, FakeCurrentUser Me, ApplicationUser User);

    private static async Task<Setup> ArrangeAsync()
    {
        var t = TestDb.Create();
        await DbSeeder.SeedAsync(t.Db);
        var user = await t.AddUserAsync("a@example.com");
        var me = new FakeCurrentUser { UserId = user.Id };
        return new Setup(t, new CategoryService(t.Db, me), me, user);
    }

    private static SaveCategoryRequest Request(string name = "Cà phê", string color = "#1f6bff", string icon = "☕") =>
        new(name, color, icon);

    [Fact]
    public async Task List_ReturnsDefaultsInSeedOrderThenOwnByName()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.CreateAsync(Request("Thú cưng", icon: "🐾"));
        await s.Service.CreateAsync(Request("Cà phê"));

        var list = await s.Service.ListAsync();

        list.Should().HaveCount(DefaultCategories.All.Count + 2);
        list.Take(DefaultCategories.All.Count).Select(c => c.Name)
            .Should().Equal(DefaultCategories.All.Select(d => d.Name));
        list.Skip(DefaultCategories.All.Count).Select(c => c.Name).Should().Equal("Cà phê", "Thú cưng");
        list.Take(DefaultCategories.All.Count).Should().OnlyContain(c => c.IsSystemDefault);
    }

    [Fact]
    public async Task List_DoesNotIncludeOtherUsersCategories_AndCountsOnlyOwnExpenses()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        s.Db.Db.Categories.Add(TestDb.NewCategory(other.Id, "Bí mật"));
        var shared = await s.Db.Db.Categories.FirstAsync(c => c.UserId == null);
        s.Db.Db.Expenses.Add(new Expense { UserId = s.User.Id, CategoryId = shared.Id, Amount = 1_000, SpentAt = new DateOnly(2026, 9, 1) });
        s.Db.Db.Expenses.Add(new Expense { UserId = other.Id, CategoryId = shared.Id, Amount = 2_000, SpentAt = new DateOnly(2026, 9, 1) });
        await s.Db.Db.SaveChangesAsync();

        var list = await s.Service.ListAsync();

        list.Should().NotContain(c => c.Name == "Bí mật");
        list.Single(c => c.Id == shared.Id).ExpenseCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_ValidRequest_NormalizesAndSaves()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.CreateAsync(Request("  Cà phê  ", "#1f6bff"));

        result.IsSuccess.Should().BeTrue();
        var saved = await s.Service.GetOwnAsync(result.Value);
        saved!.Name.Should().Be("Cà phê");
        saved.Color.Should().Be("#1F6BFF");
        saved.IsSystemDefault.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "#1F6BFF", "☕")]
    [InlineData("   ", "#1F6BFF", "☕")]
    [InlineData("Cà phê", "xanh", "☕")]
    [InlineData("Cà phê", "#12345", "☕")]
    [InlineData("Cà phê", "#1F6BFF", "khong-co-icon")]
    public async Task Create_InvalidRequest_IsRejected(string name, string color, string icon)
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.CreateAsync(new SaveCategoryRequest(name, color, icon));

        result.Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task Create_NameTooLong_IsRejected()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.CreateAsync(Request(new string('a', CategoryRules.MaxNameLength + 1)));

        result.Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task Create_NameSameAsDefaultCategory_IsRejected()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.CreateAsync(Request("Ăn uống"));

        result.Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task Create_DuplicateOwnName_IsRejected_ButOtherUserMayUseSameName()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        (await s.Service.CreateAsync(Request("Cà phê"))).IsSuccess.Should().BeTrue();
        (await s.Service.CreateAsync(Request("Cà phê"))).Status.Should().Be(ServiceStatus.Invalid);

        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new CategoryService(s.Db.Db, new FakeCurrentUser { UserId = other.Id });

        (await otherService.CreateAsync(Request("Cà phê"))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Update_OwnCategory_ChangesFields()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request("Cà phê"))).Value;

        var result = await s.Service.UpdateAsync(id, Request("Trà sữa", "#E5484D", "🎁"));

        result.IsSuccess.Should().BeTrue();
        var saved = await s.Service.GetOwnAsync(id);
        saved!.Name.Should().Be("Trà sữa");
        saved.Color.Should().Be("#E5484D");
        saved.Icon.Should().Be("🎁");
    }

    [Fact]
    public async Task Update_KeepingSameName_IsAllowed()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request("Cà phê"))).Value;

        var result = await s.Service.UpdateAsync(id, Request("Cà phê", "#E5484D"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ToNameOfAnotherOwnCategory_IsRejected()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.CreateAsync(Request("Cà phê"));
        var id = (await s.Service.CreateAsync(Request("Trà sữa"))).Value;

        var result = await s.Service.UpdateAsync(id, Request("Cà phê"));

        result.Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task UpdateAndDelete_DefaultCategory_ReturnNotFound()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var defaultId = (await s.Db.Db.Categories.FirstAsync(c => c.UserId == null)).Id;

        (await s.Service.GetOwnAsync(defaultId)).Should().BeNull();
        (await s.Service.UpdateAsync(defaultId, Request("Đổi tên"))).Status.Should().Be(ServiceStatus.NotFound);
        (await s.Service.DeleteAsync(defaultId)).Status.Should().Be(ServiceStatus.NotFound);
    }

    [Fact]
    public async Task UpdateAndDelete_OtherUsersCategory_ReturnNotFound()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request("Cà phê"))).Value;
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new CategoryService(s.Db.Db, new FakeCurrentUser { UserId = other.Id });

        (await otherService.UpdateAsync(id, Request("Hack"))).Status.Should().Be(ServiceStatus.NotFound);
        (await otherService.DeleteAsync(id)).Status.Should().Be(ServiceStatus.NotFound);
        (await s.Service.GetOwnAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_CategoryWithExpenses_IsBlockedWithConflict()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request("Cà phê"))).Value;
        s.Db.Db.Expenses.Add(new Expense { UserId = s.User.Id, CategoryId = id, Amount = 30_000, SpentAt = new DateOnly(2026, 9, 1) });
        await s.Db.Db.SaveChangesAsync();

        var result = await s.Service.DeleteAsync(id);

        result.Status.Should().Be(ServiceStatus.Conflict);
        result.Error.Should().Contain("1 khoản chi");
        (await s.Service.GetOwnAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_EmptyOwnCategory_Succeeds()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request("Cà phê"))).Value;

        var result = await s.Service.DeleteAsync(id);

        result.IsSuccess.Should().BeTrue();
        (await s.Service.GetOwnAsync(id)).Should().BeNull();
    }
}
