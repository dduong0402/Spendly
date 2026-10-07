using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Domain;
using Xunit;

namespace Spendly.Tests;

public class BudgetDataTests
{
    private static Budget NewBudget(string userId, BudgetPeriod period = BudgetPeriod.Week, long amount = 1_000_000) =>
        new() { UserId = userId, Period = period, Amount = amount };

    [Fact]
    public async Task AddBudget_SetsTimestampsFromClock_AndUpdateKeepsCreatedAt()
    {
        using var t = TestDb.Create();
        var user = await t.AddUserAsync("a@example.com");
        var budget = NewBudget(user.Id);

        t.Db.Budgets.Add(budget);
        await t.Db.SaveChangesAsync();
        var createdAt = budget.CreatedAt;
        t.Clock.UtcNow = t.Clock.UtcNow.AddHours(3);
        budget.Amount = 2_000_000;
        await t.Db.SaveChangesAsync();

        createdAt.Should().Be(new DateTimeOffset(2026, 9, 29, 3, 0, 0, TimeSpan.Zero));
        budget.CreatedAt.Should().Be(createdAt);
        budget.UpdatedAt.Should().Be(t.Clock.UtcNow);
    }

    [Fact]
    public async Task OnlyOneBudgetPerUserAndPeriod_IsAllowed()
    {
        using var t = TestDb.Create();
        var user = await t.AddUserAsync("a@example.com");
        t.Db.Budgets.Add(NewBudget(user.Id, BudgetPeriod.Week));
        await t.Db.SaveChangesAsync();

        t.Db.Budgets.Add(NewBudget(user.Id, BudgetPeriod.Week, 5_000_000));

        var act = () => t.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DifferentPeriodsOrUsers_CanEachHaveTheirOwnBudget()
    {
        using var t = TestDb.Create();
        var a = await t.AddUserAsync("a@example.com");
        var b = await t.AddUserAsync("b@example.com");

        t.Db.Budgets.AddRange(
            NewBudget(a.Id, BudgetPeriod.Week),
            NewBudget(a.Id, BudgetPeriod.Month),
            NewBudget(b.Id, BudgetPeriod.Week));
        await t.Db.SaveChangesAsync();

        (await t.Db.Budgets.CountAsync()).Should().Be(3);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task Amount_MustBePositive(long amount)
    {
        using var t = TestDb.Create();
        var user = await t.AddUserAsync("a@example.com");
        t.Db.Budgets.Add(NewBudget(user.Id, amount: amount));

        var act = () => t.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Period_IsStoredAsReadableText()
    {
        using var t = TestDb.Create();
        var user = await t.AddUserAsync("a@example.com");
        t.Db.Budgets.Add(NewBudget(user.Id, BudgetPeriod.Month));
        await t.Db.SaveChangesAsync();

        var stored = await t.Db.Database.SqlQueryRaw<string>("SELECT Period AS Value FROM Budgets").SingleAsync();

        stored.Should().Be("Month");
    }
}
