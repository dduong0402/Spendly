using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Domain;
using Xunit;

namespace Spendly.Tests;

public class CategoryBudgetDataTests
{
    private static async Task<(ApplicationUser User, Category Category)> ArrangeAsync(TestDb t, string email = "a@example.com")
    {
        var user = await t.AddUserAsync(email);
        var category = TestDb.NewCategory(user.Id, "Ăn uống");
        t.Db.Categories.Add(category);
        await t.Db.SaveChangesAsync();
        return (user, category);
    }

    private static CategoryBudget NewBudget(ApplicationUser user, Category category, BudgetPeriod period = BudgetPeriod.Month, long amount = 2_000_000) =>
        new() { UserId = user.Id, CategoryId = category.Id, Period = period, Amount = amount };

    [Fact]
    public async Task AddCategoryBudget_SetsTimestamps_AndUpdateKeepsCreatedAt()
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeAsync(t);
        var budget = NewBudget(user, category);

        t.Db.CategoryBudgets.Add(budget);
        await t.Db.SaveChangesAsync();
        var createdAt = budget.CreatedAt;
        t.Clock.UtcNow = t.Clock.UtcNow.AddHours(3);
        budget.Amount = 3_000_000;
        await t.Db.SaveChangesAsync();

        createdAt.Should().Be(new DateTimeOffset(2026, 9, 29, 3, 0, 0, TimeSpan.Zero));
        budget.CreatedAt.Should().Be(createdAt);
        budget.UpdatedAt.Should().Be(t.Clock.UtcNow);
    }

    [Fact]
    public async Task OnlyOneBudgetPerUserCategoryAndPeriod_IsAllowed()
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeAsync(t);
        t.Db.CategoryBudgets.Add(NewBudget(user, category, BudgetPeriod.Month));
        await t.Db.SaveChangesAsync();

        t.Db.CategoryBudgets.Add(NewBudget(user, category, BudgetPeriod.Month, 5_000_000));

        var act = () => t.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DifferentPeriodsOrCategoriesOrUsers_CanEachHaveTheirOwnBudget()
    {
        using var t = TestDb.Create();
        var (a, categoryA) = await ArrangeAsync(t, "a@example.com");
        var (b, _) = await ArrangeAsync(t, "b@example.com");
        var second = TestDb.NewCategory(a.Id, "Di chuyển");
        t.Db.Categories.Add(second);
        await t.Db.SaveChangesAsync();

        t.Db.CategoryBudgets.AddRange(
            NewBudget(a, categoryA, BudgetPeriod.Month),
            NewBudget(a, categoryA, BudgetPeriod.Week),
            NewBudget(a, second, BudgetPeriod.Month));
        // Người dùng khác đặt hạn mức cho danh mục mặc định (UserId = null) cùng kỳ vẫn được.
        var shared = new Category { UserId = null, Name = "Mặc định", Color = "#1F6BFF", Icon = "📌" };
        t.Db.Categories.Add(shared);
        await t.Db.SaveChangesAsync();
        t.Db.CategoryBudgets.AddRange(
            new CategoryBudget { UserId = a.Id, CategoryId = shared.Id, Period = BudgetPeriod.Month, Amount = 1_000_000 },
            new CategoryBudget { UserId = b.Id, CategoryId = shared.Id, Period = BudgetPeriod.Month, Amount = 1_000_000 });
        await t.Db.SaveChangesAsync();

        (await t.Db.CategoryBudgets.CountAsync()).Should().Be(5);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task Amount_MustBePositive(long amount)
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeAsync(t);
        t.Db.CategoryBudgets.Add(NewBudget(user, category, amount: amount));

        var act = () => t.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CategoryWithABudget_CannotBeDeletedDirectly()
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeAsync(t);
        t.Db.CategoryBudgets.Add(NewBudget(user, category));
        await t.Db.SaveChangesAsync();

        using var other = t.NewContext();
        var toDelete = await other.Categories.FirstAsync(c => c.Id == category.Id);
        other.Categories.Remove(toDelete);

        var act = () => other.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Period_IsStoredAsReadableText()
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeAsync(t);
        t.Db.CategoryBudgets.Add(NewBudget(user, category, BudgetPeriod.Week));
        await t.Db.SaveChangesAsync();

        var stored = await t.Db.Database.SqlQueryRaw<string>("SELECT Period AS Value FROM CategoryBudgets").SingleAsync();

        stored.Should().Be("Week");
    }
}
