using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Domain;
using Xunit;

namespace Spendly.Tests;

public class AppDbContextTests
{
    private static async Task<(ApplicationUser User, Category Category)> ArrangeUserAndCategoryAsync(TestDb t)
    {
        var user = TestDb.NewUser();
        var category = TestDb.NewCategory(user.Id);
        t.Db.Users.Add(user);
        t.Db.Categories.Add(category);
        await t.Db.SaveChangesAsync();
        return (user, category);
    }

    private static Expense NewExpense(ApplicationUser user, Category category, long amount = 50_000) => new()
    {
        UserId = user.Id,
        CategoryId = category.Id,
        Amount = amount,
        SpentAt = new DateOnly(2026, 9, 29),
        Note = "Phở bò"
    };

    [Fact]
    public async Task AddExpense_SetsCreatedAtAndUpdatedAtFromClock()
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeUserAndCategoryAsync(t);
        var expense = NewExpense(user, category);

        t.Db.Expenses.Add(expense);
        await t.Db.SaveChangesAsync();

        expense.CreatedAt.Should().Be(t.Clock.UtcNow);
        expense.UpdatedAt.Should().Be(t.Clock.UtcNow);
    }

    [Fact]
    public async Task UpdateExpense_ChangesUpdatedAtButKeepsCreatedAt()
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeUserAndCategoryAsync(t);
        var expense = NewExpense(user, category);
        t.Db.Expenses.Add(expense);
        await t.Db.SaveChangesAsync();
        var createdAt = expense.CreatedAt;

        t.Clock.UtcNow = t.Clock.UtcNow.AddHours(2);
        expense.Amount = 60_000;
        await t.Db.SaveChangesAsync();

        expense.CreatedAt.Should().Be(createdAt);
        expense.UpdatedAt.Should().Be(t.Clock.UtcNow);
    }

    [Fact]
    public async Task AddUser_SetsCreatedAt()
    {
        using var t = TestDb.Create();
        var user = TestDb.NewUser();

        t.Db.Users.Add(user);
        await t.Db.SaveChangesAsync();

        user.CreatedAt.Should().Be(t.Clock.UtcNow);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1000L)]
    public async Task AddExpense_WithNonPositiveAmount_IsRejectedByDatabase(long amount)
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeUserAndCategoryAsync(t);

        t.Db.Expenses.Add(NewExpense(user, category, amount));

        var act = () => t.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AddCategory_WithDuplicateNameForSameUser_IsRejected()
    {
        using var t = TestDb.Create();
        var (user, _) = await ArrangeUserAndCategoryAsync(t);

        t.Db.Categories.Add(TestDb.NewCategory(user.Id, "Ăn uống"));

        var act = () => t.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeleteCategory_WithExpenses_IsRejected()
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeUserAndCategoryAsync(t);
        t.Db.Expenses.Add(NewExpense(user, category));
        await t.Db.SaveChangesAsync();

        // Dùng context mới để không có expense nào đang được theo dõi.
        using var other = t.NewContext();
        var toDelete = await other.Categories.FirstAsync(c => c.Id == category.Id);
        other.Categories.Remove(toDelete);

        var act = () => other.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SpentAt_RoundTripsAsDateOnly()
    {
        using var t = TestDb.Create();
        var (user, category) = await ArrangeUserAndCategoryAsync(t);
        var expense = NewExpense(user, category);
        t.Db.Expenses.Add(expense);
        await t.Db.SaveChangesAsync();

        using var other = t.NewContext();
        var loaded = await other.Expenses.SingleAsync(e => e.Id == expense.Id);

        loaded.SpentAt.Should().Be(new DateOnly(2026, 9, 29));
    }
}
