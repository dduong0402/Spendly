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

public class CategoryIconServiceTests
{
    private static async Task<(TestDb Db, CategoryService Service)> ArrangeAsync()
    {
        var t = TestDb.Create();
        var user = await t.AddUserAsync("a@example.com");
        return (t, new CategoryService(t.Db, new FakeCurrentUser { UserId = user.Id }));
    }

    [Fact]
    public async Task Create_WithAnyEmoji_IsAccepted()
    {
        var (db, service) = await ArrangeAsync();
        using var _ = db;

        var result = await service.CreateAsync(new SaveCategoryRequest("Thú cưng", "#1F6BFF", "🐶"));

        result.IsSuccess.Should().BeTrue();
        (await service.GetOwnAsync(result.Value))!.Icon.Should().Be("🐶");
    }

    [Fact]
    public async Task Create_TrimsSurroundingWhitespaceOfIcon()
    {
        var (db, service) = await ArrangeAsync();
        using var _ = db;

        var result = await service.CreateAsync(new SaveCategoryRequest("Cà phê", "#1F6BFF", "  ☕ "));

        result.IsSuccess.Should().BeTrue();
        (await service.GetOwnAsync(result.Value))!.Icon.Should().Be("☕");
    }

    [Theory]
    [InlineData("coffee")]
    [InlineData("😀😀")]
    [InlineData("ab")]
    public async Task Create_NotExactlyOneEmoji_IsRejected(string icon)
    {
        var (db, service) = await ArrangeAsync();
        using var _ = db;

        var result = await service.CreateAsync(new SaveCategoryRequest("Cà phê", "#1F6BFF", icon));

        result.Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task Update_ToAnotherEmoji_Works()
    {
        var (db, service) = await ArrangeAsync();
        using var _ = db;
        var id = (await service.CreateAsync(new SaveCategoryRequest("Cà phê", "#1F6BFF", "☕"))).Value;

        var result = await service.UpdateAsync(id, new SaveCategoryRequest("Cà phê", "#1F6BFF", "🧋"));

        result.IsSuccess.Should().BeTrue();
        (await service.GetOwnAsync(id))!.Icon.Should().Be("🧋");
    }

    [Fact]
    public async Task Seed_ConvertsLegacyIconKeysToEmoji_ForDefaultAndUserCategories()
    {
        var t = TestDb.Create();
        using var _ = t;
        var user = await t.AddUserAsync("a@example.com");
        t.Db.Categories.Add(new Category { UserId = null, Name = "Ăn uống", Color = "#1F6BFF", Icon = "utensils" });
        t.Db.Categories.Add(new Category { UserId = user.Id, Name = "Cà phê", Color = "#1F6BFF", Icon = "coffee" });
        await t.Db.SaveChangesAsync();

        await DbSeeder.SeedAsync(t.Db);

        (await t.Db.Categories.SingleAsync(c => c.Name == "Ăn uống" && c.UserId == null)).Icon.Should().Be("🍽️");
        (await t.Db.Categories.SingleAsync(c => c.Name == "Cà phê")).Icon.Should().Be("☕");
        (await t.Db.Categories.CountAsync(c => c.UserId == null)).Should().Be(DefaultCategories.All.Count);
    }

    [Fact]
    public async Task Seed_DefaultCategories_AllHaveValidEmojiIcons()
    {
        var t = TestDb.Create();
        using var _ = t;

        await DbSeeder.SeedAsync(t.Db);

        var icons = await t.Db.Categories.Where(c => c.UserId == null).Select(c => c.Icon).ToListAsync();
        icons.Should().OnlyContain(icon => CategoryIcons.IsValid(icon));
    }

    [Fact]
    public async Task Seed_DoesNotTouchUserChosenEmoji()
    {
        var t = TestDb.Create();
        using var _ = t;
        var user = await t.AddUserAsync("a@example.com");
        t.Db.Categories.Add(new Category { UserId = user.Id, Name = "Mèo", Color = "#1F6BFF", Icon = "🐱" });
        await t.Db.SaveChangesAsync();

        await DbSeeder.SeedAsync(t.Db);

        (await t.Db.Categories.SingleAsync(c => c.Name == "Mèo")).Icon.Should().Be("🐱");
    }
}
