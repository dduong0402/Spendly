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
public class BudgetServiceTests
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

    private static async Task AddExpenseAsync(Setup s, DateOnly date, long amount, Category? category = null, ApplicationUser? user = null)
    {
        s.Db.Db.Expenses.Add(new Expense
        {
            UserId = (user ?? s.User).Id,
            CategoryId = (category ?? s.Food).Id,
            Amount = amount,
            SpentAt = date
        });
        await s.Db.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetStatuses_WithoutBudgets_ReturnsEmptyList()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        (await s.Service.GetStatusesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Set_CreatesBudget_ThenUpdatesTheSameRow()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        (await s.Service.SetAsync(BudgetPeriod.Week, 1_000_000)).IsSuccess.Should().BeTrue();
        (await s.Service.SetAsync(BudgetPeriod.Week, 2_000_000)).IsSuccess.Should().BeTrue();

        var rows = await s.Db.Db.Budgets.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Amount.Should().Be(2_000_000);
        rows[0].UserId.Should().Be(s.User.Id);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    [InlineData(ExpenseRules.MaxAmount + 1)]
    public async Task Set_WithInvalidAmount_IsRejected(long amount)
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.SetAsync(BudgetPeriod.Month, amount);

        result.Status.Should().Be(ServiceStatus.Invalid);
        (await s.Db.Db.Budgets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Set_WithUnknownPeriod_IsRejected()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.SetAsync((BudgetPeriod)99, 1_000_000);

        result.Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task GetStatuses_ReturnsWeekThenMonth_WithSpendingInsideCurrentRangeOnly()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetAsync(BudgetPeriod.Month, 3_000_000); // cố ý đặt tháng trước
        await s.Service.SetAsync(BudgetPeriod.Week, 1_000_000);
        await AddExpenseAsync(s, new DateOnly(2026, 8, 31), 999_000); // tháng trước: không tính
        await AddExpenseAsync(s, new DateOnly(2026, 9, 1), 200_000);  // chỉ thuộc tháng
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 100_000); // thuộc cả tuần và tháng

        var statuses = await s.Service.GetStatusesAsync();

        statuses.Select(x => x.Period).Should().Equal(BudgetPeriod.Week, BudgetPeriod.Month);
        statuses[0].Spent.Should().Be(100_000);
        statuses[0].Limit.Should().Be(1_000_000);
        statuses[1].Spent.Should().Be(300_000);
        statuses[1].Limit.Should().Be(3_000_000);
    }

    [Fact]
    public async Task GetStatuses_OnlyCountsCurrentUsersExpensesAndBudgets()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new BudgetService(s.Db.Db, new FakeCurrentUser { UserId = other.Id }, s.Db.Clock);
        await s.Service.SetAsync(BudgetPeriod.Week, 1_000_000);
        await otherService.SetAsync(BudgetPeriod.Week, 50_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 100_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 900_000, user: other);

        var mine = await s.Service.GetStatusesAsync();
        var theirs = await otherService.GetStatusesAsync();

        mine.Should().ContainSingle().Which.Spent.Should().Be(100_000);
        mine[0].Limit.Should().Be(1_000_000);
        theirs.Should().ContainSingle().Which.Spent.Should().Be(900_000);
        theirs[0].Level.Should().Be(BudgetLevel.Exceeded);
    }

    [Fact]
    public async Task GetStatuses_AtHalfOrMore_IncludesTopCategory()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetAsync(BudgetPeriod.Week, 200_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 150_000, s.Food);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 29), 50_000, s.Transport);

        var status = (await s.Service.GetStatusesAsync()).Single();

        status.Level.Should().Be(BudgetLevel.Reached);
        status.TopCategoryName.Should().Be(s.Food.Name);
        status.TopCategoryPercent.Should().Be(75);
        status.Advice.Should().Contain(s.Food.Name);
    }

    [Fact]
    public async Task GetStatuses_WhenSafe_HasNoTopCategory()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetAsync(BudgetPeriod.Week, 1_000_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 100_000);

        var status = (await s.Service.GetStatusesAsync()).Single();

        status.Level.Should().Be(BudgetLevel.Safe);
        status.TopCategoryName.Should().BeNull();
    }

    [Fact]
    public async Task Delete_RemovesBudget_ThenReturnsNotFound()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetAsync(BudgetPeriod.Week, 1_000_000);

        (await s.Service.DeleteAsync(BudgetPeriod.Week)).IsSuccess.Should().BeTrue();
        (await s.Service.DeleteAsync(BudgetPeriod.Week)).Status.Should().Be(ServiceStatus.NotFound);
        (await s.Service.GetStatusesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_DoesNotTouchOtherUsersBudget()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new BudgetService(s.Db.Db, new FakeCurrentUser { UserId = other.Id }, s.Db.Clock);
        await otherService.SetAsync(BudgetPeriod.Week, 1_000_000);

        (await s.Service.DeleteAsync(BudgetPeriod.Week)).Status.Should().Be(ServiceStatus.NotFound);
        (await otherService.GetStatusesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task AddingAnExpense_CrossingAThreshold_ProducesAnAlert_EndToEnd()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetAsync(BudgetPeriod.Week, 1_000_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 400_000);
        var before = await s.Service.GetStatusesAsync();

        await AddExpenseAsync(s, new DateOnly(2026, 9, 29), 400_000); // tổng 800.000 = 80%
        var after = await s.Service.GetStatusesAsync();
        var alerts = BudgetAlertPolicy.Detect(before, after);

        alerts.Should().ContainSingle().Which.Title.Should().Contain("75%");
    }

    [Fact]
    public async Task AddingAnExpenseOutsideTheCurrentWeek_ProducesNoAlert()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await s.Service.SetAsync(BudgetPeriod.Week, 1_000_000);
        await AddExpenseAsync(s, new DateOnly(2026, 9, 28), 900_000);
        var before = await s.Service.GetStatusesAsync();

        await AddExpenseAsync(s, new DateOnly(2026, 9, 20), 900_000); // tuần trước
        var after = await s.Service.GetStatusesAsync();

        BudgetAlertPolicy.Detect(before, after).Should().BeEmpty();
    }
}
