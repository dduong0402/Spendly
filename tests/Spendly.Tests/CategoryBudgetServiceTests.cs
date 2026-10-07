using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Common;
using Spendly.Web.Data.Seed;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

/// <summary>Đồng hồ test: Thứ Ba 29/09/2026 (giờ Việt Nam). Tuần hiện tại: 28/09 - 04/10.</summary>
public class CategoryBudgetServiceTests
{
    private sealed record Setup(TestDb Db, BudgetService Service, ApplicationUser User, Category Food, Category Transport);

    private static async Task<Setup> ArrangeAsync()
    {
        var t = TestDb.Create();
        await DbSeeder.SeedAsync(t.Db);
        var user = await t.AddUserAsync("a@example.com");
        var categories = await t.Db.Categories.Where(c => c.UserId == null).OrderBy(c => c.Id).Take(2).ToListAsync();
        var service = new BudgetService(t.Db, new FakeCurrentUser { UserId = user.Id }, t.Clock);
        return new Setup(t, service, user, categories[0], categories[1]);
    }

    private static async Task AddExpenseAsync(Setup s, DateOnly date, long amount, Category category, ApplicationUser? user = null)
    {
        s.Db.Db.Expenses.Add(new Expense
        {
            UserId = (user ?? s.User).Id,
            CategoryId = category.Id,
            Amount = amount,
            SpentAt = date
        });
        await s.Db.Db.SaveChangesAsync();
    }

    // ---------- Đặt / sửa / xóa ----------

    [Fact]
    public async Task SetCategoryBudget_Creates_ThenUpdatesTheSameRow()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        (await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month, 2_000_000)).IsSuccess.Should().BeTrue();
        (await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month, 2_500_000)).IsSuccess.Should().BeTrue();

        var rows = await s.Db.Db.CategoryBudgets.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Amount.Should().Be(2_500_000);
        rows[0].CategoryId.Should().Be(s.Food.Id);
        rows[0].UserId.Should().Be(s.User.Id);
    }

    [Fact]
    public async Task SetCategoryBudget_WeekAndMonthForSameCategory_Coexist()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Week, 500_000);
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month, 2_000_000);

        (await s.Db.Db.CategoryBudgets.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    [InlineData(ExpenseRules.MaxAmount + 1)]
    public async Task SetCategoryBudget_WithInvalidAmount_IsRejected(long amount)
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month, amount);

        result.Status.Should().Be(ServiceStatus.Invalid);
        (await s.Db.Db.CategoryBudgets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SetCategoryBudget_WithUnknownPeriod_IsRejected()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        (await s.Service.SetCategoryBudgetAsync(s.Food.Id, (BudgetPeriod)99, 1_000_000)).Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task SetCategoryBudget_ForNonexistentOrForeignCategory_IsRejected()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var foreign = TestDb.NewCategory(other.Id, "Của người khác");
        s.Db.Db.Categories.Add(foreign);
        await s.Db.Db.SaveChangesAsync();

        (await s.Service.SetCategoryBudgetAsync(99_999, BudgetPeriod.Month, 1_000_000)).Status.Should().Be(ServiceStatus.Invalid);
        (await s.Service.SetCategoryBudgetAsync(foreign.Id, BudgetPeriod.Month, 1_000_000)).Status.Should().Be(ServiceStatus.Invalid);
        (await s.Db.Db.CategoryBudgets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SetCategoryBudget_ForOwnCategory_IsAllowed()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var own = TestDb.NewCategory(s.User.Id, "Cà phê");
        s.Db.Db.Categories.Add(own);
        await s.Db.Db.SaveChangesAsync();

        (await s.Service.SetCategoryBudgetAsync(own.Id, BudgetPeriod.Week, 300_000)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCategoryBudget_RemovesIt_ThenReturnsNotFound()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month, 2_000_000);

        (await s.Service.DeleteCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month)).IsSuccess.Should().BeTrue();
        (await s.Service.DeleteCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month)).Status.Should().Be(ServiceStatus.NotFound);
        (await s.Service.GetCategoryStatusesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCategoryBudget_DoesNotTouchOtherUsersBudget()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new BudgetService(s.Db.Db, new FakeCurrentUser { UserId = other.Id }, s.Db.Clock);
        await otherService.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month, 1_000_000);

        (await s.Service.DeleteCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month)).Status.Should().Be(ServiceStatus.NotFound);
        (await otherService.GetCategoryStatusesAsync()).Should().ContainSingle();
    }

    // ---------- Tình hình ----------

    [Fact]
    public async Task GetCategoryStatuses_WithoutBudgets_ReturnsEmpty()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        (await s.Service.GetCategoryStatusesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategoryStatuses_CountsOnlyThatCategoryWithinCurrentRange()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Week, 200_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 100_000, s.Food);       // tuần này, đúng danh mục
        await AddExpenseAsync(s, new DateOnly(2026, 9, 20), 900_000, s.Food);       // tuần trước: không tính
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 50_000, s.Transport);   // tuần này, danh mục khác: không tính

        var item = (await s.Service.GetCategoryStatusesAsync()).Single();

        item.CategoryId.Should().Be(s.Food.Id);
        item.CategoryName.Should().Be(s.Food.Name);
        item.CategoryColor.Should().Be(s.Food.Color);
        item.CategoryEmoji.Should().NotBeNullOrEmpty();
        item.Status.Spent.Should().Be(100_000);
        item.Status.Limit.Should().Be(200_000);
        item.Status.Level.Should().Be(BudgetLevel.Half);
        item.Status.PeriodLabel.Should().Be($"Hạn mức tuần · {s.Food.Name}");
        item.Status.TopCategoryName.Should().BeNull();
    }

    [Fact]
    public async Task GetCategoryStatuses_WeekAndMonthRangesAreIndependent()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Week, 1_000_000);
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month, 3_000_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 1), 200_000, s.Food);   // chỉ thuộc tháng
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 100_000, s.Food);  // thuộc cả tuần và tháng

        var statuses = await s.Service.GetCategoryStatusesAsync();

        statuses.Single(x => x.Status.Period == BudgetPeriod.Week).Status.Spent.Should().Be(100_000);
        statuses.Single(x => x.Status.Period == BudgetPeriod.Month).Status.Spent.Should().Be(300_000);
    }

    [Fact]
    public async Task GetCategoryStatuses_AreSortedByPercentDescending()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Week, 1_000_000);      // 10%
        await s.Service.SetCategoryBudgetAsync(s.Transport.Id, BudgetPeriod.Week, 100_000);   // 90%
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 100_000, s.Food);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 90_000, s.Transport);

        var statuses = await s.Service.GetCategoryStatusesAsync();

        statuses.Select(x => x.CategoryId).Should().Equal(s.Transport.Id, s.Food.Id);
        statuses[0].Status.Level.Should().Be(BudgetLevel.Nearly);
    }

    [Fact]
    public async Task GetCategoryStatuses_OnlyForCurrentUser()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new BudgetService(s.Db.Db, new FakeCurrentUser { UserId = other.Id }, s.Db.Clock);
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Week, 1_000_000);
        await otherService.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Week, 50_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 100_000, s.Food);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 900_000, s.Food, user: other);

        var mine = await s.Service.GetCategoryStatusesAsync();
        var theirs = await otherService.GetCategoryStatusesAsync();

        mine.Should().ContainSingle().Which.Status.Spent.Should().Be(100_000);
        theirs.Should().ContainSingle().Which.Status.Spent.Should().Be(900_000);
        theirs[0].Status.Level.Should().Be(BudgetLevel.Exceeded);
    }

    [Fact]
    public async Task GetSnapshot_CombinesOverallAndCategoryBudgets()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetAsync(BudgetPeriod.Month, 5_000_000);
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Month, 2_000_000);

        var snapshot = await s.Service.GetSnapshotAsync();

        snapshot.Overall.Should().ContainSingle().Which.Period.Should().Be(BudgetPeriod.Month);
        snapshot.Categories.Should().ContainSingle().Which.CategoryId.Should().Be(s.Food.Id);
    }

    // ---------- Cảnh báo từ đầu đến cuối ----------

    [Fact]
    public async Task AddingAnExpense_CrossingCategoryThreshold_RaisesCategoryAlertOnly()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetAsync(BudgetPeriod.Week, 10_000_000);                          // hạn mức tổng rất lớn
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Week, 100_000);
        var before = await s.Service.GetSnapshotAsync();

        await AddExpenseAsync(s, new DateOnly(2026, 9, 29), 60_000, s.Food);              // 60% hạn mức Ăn uống
        var after = await s.Service.GetSnapshotAsync();
        var alerts = BudgetAlertPolicy.Detect(before, after);

        var alert = alerts.Should().ContainSingle().Subject;
        alert.Title.Should().Contain($"Hạn mức tuần · {s.Food.Name}");
    }

    [Fact]
    public async Task AddingAnExpenseInAnotherCategory_DoesNotAlertTheBudgetedCategory()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetCategoryBudgetAsync(s.Food.Id, BudgetPeriod.Week, 100_000);
        var before = await s.Service.GetSnapshotAsync();

        await AddExpenseAsync(s, new DateOnly(2026, 9, 29), 500_000, s.Transport);
        var after = await s.Service.GetSnapshotAsync();

        BudgetAlertPolicy.Detect(before, after).Should().BeEmpty();
    }

    [Fact]
    public async Task MovingAnExpenseIntoABudgetedCategory_RaisesAlert()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetCategoryBudgetAsync(s.Transport.Id, BudgetPeriod.Week, 100_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 29), 80_000, s.Food);
        var before = await s.Service.GetSnapshotAsync();

        var expense = await s.Db.Db.Expenses.SingleAsync();
        expense.CategoryId = s.Transport.Id; // sửa khoản chi sang danh mục khác
        await s.Db.Db.SaveChangesAsync();
        var after = await s.Service.GetSnapshotAsync();

        var alert = BudgetAlertPolicy.Detect(before, after).Should().ContainSingle().Subject;
        alert.Title.Should().Contain(s.Transport.Name).And.Contain("75%");
    }

    // ---------- Xóa danh mục ----------

    [Fact]
    public async Task DeletingAnOwnCategory_AlsoRemovesItsCategoryBudgets()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var own = TestDb.NewCategory(s.User.Id, "Cà phê");
        s.Db.Db.Categories.Add(own);
        await s.Db.Db.SaveChangesAsync();
        await s.Service.SetCategoryBudgetAsync(own.Id, BudgetPeriod.Week, 300_000);
        await s.Service.SetCategoryBudgetAsync(own.Id, BudgetPeriod.Month, 1_000_000);
        var categoryService = new CategoryService(s.Db.Db, new FakeCurrentUser { UserId = s.User.Id });

        var result = await categoryService.DeleteAsync(own.Id);

        result.IsSuccess.Should().BeTrue();
        (await s.Db.Db.CategoryBudgets.CountAsync()).Should().Be(0);
        (await s.Db.Db.Categories.AnyAsync(c => c.Id == own.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingACategoryWithExpenses_IsStillBlocked_AndKeepsItsBudgets()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var own = TestDb.NewCategory(s.User.Id, "Cà phê");
        s.Db.Db.Categories.Add(own);
        await s.Db.Db.SaveChangesAsync();
        await s.Service.SetCategoryBudgetAsync(own.Id, BudgetPeriod.Week, 300_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 30_000, own);
        var categoryService = new CategoryService(s.Db.Db, new FakeCurrentUser { UserId = s.User.Id });

        var result = await categoryService.DeleteAsync(own.Id);

        result.Status.Should().Be(ServiceStatus.Conflict);
        (await s.Db.Db.CategoryBudgets.CountAsync()).Should().Be(1);
    }
}
