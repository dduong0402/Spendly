using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Common;
using Spendly.Web.Data.Seed;
using Spendly.Web.Domain;
using Spendly.Web.Models.Expenses;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class ExpenseServiceTests
{
    // Đồng hồ test mặc định: 29/09/2026 (giờ Việt Nam).
    private static readonly DateOnly Today = new(2026, 9, 29);

    private sealed record Setup(TestDb Db, ExpenseService Service, FakeCurrentUser Me, ApplicationUser User, Category Category);

    private static async Task<Setup> ArrangeAsync()
    {
        var t = TestDb.Create();
        await DbSeeder.SeedAsync(t.Db);
        var user = await t.AddUserAsync("a@example.com");
        var me = new FakeCurrentUser { UserId = user.Id };
        var category = await t.Db.Categories.OrderBy(c => c.Id).FirstAsync(c => c.UserId == null);
        return new Setup(t, new ExpenseService(t.Db, me, t.Clock), me, user, category);
    }

    private static SaveExpenseRequest Request(Category category, long amount = 50_000, DateOnly? spentAt = null, string? note = "Phở bò") =>
        new(amount, category.Id, spentAt ?? Today, note);

    [Fact]
    public async Task Create_ThenList_ReturnsExpenseWithCategoryInfo()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var created = await s.Service.CreateAsync(Request(s.Category));
        var list = await s.Service.ListAsync(new ExpenseFilter());

        created.IsSuccess.Should().BeTrue();
        list.TotalCount.Should().Be(1);
        var item = list.Items.Single();
        item.Id.Should().Be(created.Value);
        item.Amount.Should().Be(50_000);
        item.Note.Should().Be("Phở bò");
        item.CategoryName.Should().Be(s.Category.Name);
        item.SpentAt.Should().Be(Today);
    }

    [Fact]
    public async Task Create_TrimsNote_AndBlankNoteBecomesNull()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var a = await s.Service.CreateAsync(Request(s.Category, note: "  cà phê  "));
        var b = await s.Service.CreateAsync(Request(s.Category, note: "   "));

        (await s.Service.GetAsync(a.Value)).Should().Match<ExpenseDetail>(d => d.Note == "cà phê");
        (await s.Service.GetAsync(b.Value)).Should().Match<ExpenseDetail>(d => d.Note == null);
    }

    [Fact]
    public async Task Create_WithZeroAmount_IsInvalid()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.CreateAsync(Request(s.Category, amount: 0));

        result.Status.Should().Be(ServiceStatus.Invalid);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_WithFutureDate_IsInvalid_ButTodayIsOk()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        (await s.Service.CreateAsync(Request(s.Category, spentAt: Today.AddDays(1)))).Status.Should().Be(ServiceStatus.Invalid);
        (await s.Service.CreateAsync(Request(s.Category, spentAt: Today))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_UsesVietnamDateForToday()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        // 18:00 UTC ngày 29/09 = 01:00 ngày 30/09 tại Việt Nam.
        s.Db.Clock.UtcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);

        var result = await s.Service.CreateAsync(Request(s.Category, spentAt: new DateOnly(2026, 9, 30)));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithOtherUsersCategory_IsInvalid()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var foreign = TestDb.NewCategory(other.Id, "Của người khác");
        s.Db.Db.Categories.Add(foreign);
        await s.Db.Db.SaveChangesAsync();

        var result = await s.Service.CreateAsync(Request(foreign));

        result.Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task Create_WithOwnCategory_IsOk()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var own = TestDb.NewCategory(s.User.Id, "Cà phê");
        s.Db.Db.Categories.Add(own);
        await s.Db.Db.SaveChangesAsync();

        var result = await s.Service.CreateAsync(Request(own));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task List_OnlyReturnsCurrentUsersExpenses()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.CreateAsync(Request(s.Category));
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new ExpenseService(s.Db.Db, new FakeCurrentUser { UserId = other.Id }, s.Db.Clock);
        await otherService.CreateAsync(Request(s.Category, amount: 999_000));

        var mine = await s.Service.ListAsync(new ExpenseFilter());
        var theirs = await otherService.ListAsync(new ExpenseFilter());

        mine.TotalCount.Should().Be(1);
        mine.TotalAmount.Should().Be(50_000);
        theirs.TotalAmount.Should().Be(999_000);
    }

    [Fact]
    public async Task List_FiltersByDateRange_AndOrdersNewestFirst()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        foreach (var day in new[] { 25, 27, 29 })
        {
            await s.Service.CreateAsync(Request(s.Category, spentAt: new DateOnly(2026, 9, day)));
        }

        var result = await s.Service.ListAsync(new ExpenseFilter { From = new DateOnly(2026, 9, 26), To = new DateOnly(2026, 9, 29) });

        result.Items.Select(i => i.SpentAt.Day).Should().Equal(29, 27);
    }

    [Fact]
    public async Task List_WithReversedDateRange_SwapsBounds()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.CreateAsync(Request(s.Category, spentAt: new DateOnly(2026, 9, 27)));

        var result = await s.Service.ListAsync(new ExpenseFilter { From = new DateOnly(2026, 9, 29), To = new DateOnly(2026, 9, 26) });

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task List_FiltersByCategory()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.Db.Categories.Where(c => c.UserId == null && c.Id != s.Category.Id).OrderBy(c => c.Id).FirstAsync();
        await s.Service.CreateAsync(Request(s.Category));
        await s.Service.CreateAsync(Request(other));

        var result = await s.Service.ListAsync(new ExpenseFilter { CategoryId = other.Id });

        result.TotalCount.Should().Be(1);
        result.Items.Single().CategoryId.Should().Be(other.Id);
    }

    [Fact]
    public async Task List_FiltersByKeywordInNoteOrCategoryName()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.CreateAsync(Request(s.Category, note: "cafe sáng"));
        await s.Service.CreateAsync(Request(s.Category, note: "xăng xe"));

        // Lưu ý: SQLite phân biệt hoa/thường ở Contains; SQL Server (collation mặc định) thì không.
        var byNote = await s.Service.ListAsync(new ExpenseFilter { Q = "cafe" });
        var byCategory = await s.Service.ListAsync(new ExpenseFilter { Q = s.Category.Name });

        byNote.TotalCount.Should().Be(1);
        byCategory.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task List_Paging_ReturnsPageTotalsAndClampsPage()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        for (var i = 1; i <= 12; i++)
        {
            await s.Service.CreateAsync(Request(s.Category, amount: i * 1_000));
        }

        var page3 = await s.Service.ListAsync(new ExpenseFilter { Page = 3, PageSize = 5 });
        var beyond = await s.Service.ListAsync(new ExpenseFilter { Page = 99, PageSize = 5 });

        page3.Items.Should().HaveCount(2);
        page3.TotalCount.Should().Be(12);
        page3.TotalAmount.Should().Be(78_000);
        page3.TotalPages.Should().Be(3);
        beyond.Page.Should().Be(3);
        beyond.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_WhenEmpty_ReturnsPageOneWithZeroTotals()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.ListAsync(new ExpenseFilter { Page = 5 });

        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.TotalPages.Should().Be(1);
        result.TotalAmount.Should().Be(0);
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request(s.Category))).Value;

        var result = await s.Service.UpdateAsync(id, Request(s.Category, amount: 80_000, spentAt: new DateOnly(2026, 9, 28), note: "Bún chả"));

        result.IsSuccess.Should().BeTrue();
        var detail = await s.Service.GetAsync(id);
        detail!.Amount.Should().Be(80_000);
        detail.SpentAt.Should().Be(new DateOnly(2026, 9, 28));
        detail.Note.Should().Be("Bún chả");
    }

    [Fact]
    public async Task Update_WithInvalidAmount_IsRejectedAndKeepsOldValue()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request(s.Category))).Value;

        var result = await s.Service.UpdateAsync(id, Request(s.Category, amount: -5));

        result.Status.Should().Be(ServiceStatus.Invalid);
        (await s.Service.GetAsync(id))!.Amount.Should().Be(50_000);
    }

    [Fact]
    public async Task UpdateGetDelete_OnOtherUsersExpense_ReturnNotFound()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request(s.Category))).Value;
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new ExpenseService(s.Db.Db, new FakeCurrentUser { UserId = other.Id }, s.Db.Clock);

        (await otherService.GetAsync(id)).Should().BeNull();
        (await otherService.UpdateAsync(id, Request(s.Category, amount: 1))).Status.Should().Be(ServiceStatus.NotFound);
        (await otherService.DeleteAsync(id)).Status.Should().Be(ServiceStatus.NotFound);
        (await s.Service.GetAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_RemovesExpense()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = (await s.Service.CreateAsync(Request(s.Category))).Value;

        var result = await s.Service.DeleteAsync(id);

        result.IsSuccess.Should().BeTrue();
        (await s.Service.GetAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Service_WhenNotLoggedIn_Throws()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        s.Me.UserId = null;

        var act = () => s.Service.ListAsync(new ExpenseFilter());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
