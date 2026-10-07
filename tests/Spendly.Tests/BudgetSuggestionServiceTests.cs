using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Data.Seed;
using Spendly.Web.Domain;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

/// <summary>Đồng hồ test: Thứ Ba 29/09/2026 (giờ Việt Nam).</summary>
public class BudgetSuggestionServiceTests
{
    private sealed record Setup(TestDb Db, BudgetSuggestionService Service, ApplicationUser User, Category Food, Category Transport);

    private static async Task<Setup> ArrangeAsync()
    {
        var t = TestDb.Create();
        await DbSeeder.SeedAsync(t.Db);
        var user = await t.AddUserAsync("a@example.com");
        var categories = await t.Db.Categories.Where(c => c.UserId == null).OrderBy(c => c.Id).Take(2).ToListAsync();
        var service = new BudgetSuggestionService(t.Db, new FakeCurrentUser { UserId = user.Id }, t.Clock);
        return new Setup(t, service, user, categories[0], categories[1]);
    }

    private static async Task AddAsync(Setup s, DateOnly date, long amount, Category category, ApplicationUser? user = null)
    {
        s.Db.Db.Expenses.Add(new Expense { UserId = (user ?? s.User).Id, CategoryId = category.Id, Amount = amount, SpentAt = date });
        await s.Db.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task NoExpenses_ReturnsEmpty()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var result = await s.Service.GetSuggestionsAsync();

        result.Overall.Should().BeEmpty();
        result.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyCurrentPeriodExpenses_ReturnsEmpty()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 29), 5_000_000, s.Food);

        var result = await s.Service.GetSuggestionsAsync();

        result.Overall.Should().BeEmpty();
        result.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task WeeklySuggestion_UsesLastThreeFullWeeks_AndIgnoresCurrentWeek()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 7), 1_000_000, s.Food);   // tuần 07/09
        await AddAsync(s, new DateOnly(2026, 9, 15), 600_000, s.Food);    // tuần 14/09
        await AddAsync(s, new DateOnly(2026, 9, 16), 500_000, s.Transport);
        await AddAsync(s, new DateOnly(2026, 9, 22), 1_150_000, s.Food);  // tuần 21/09
        await AddAsync(s, new DateOnly(2026, 9, 29), 9_000_000, s.Food);  // tuần hiện tại: bỏ qua

        var result = await s.Service.GetSuggestionsAsync();

        var overall = result.ForOverall(BudgetPeriod.Week)!;
        overall.PeriodsUsed.Should().Be(3);
        overall.Average.Should().Be(1_083_333);
        overall.Suggested.Should().Be(1_100_000);
        overall.Saving.Should().Be(1_000_000);
        overall.Samples.Select(x => x.Amount).Should().Equal(1_000_000, 1_100_000, 1_150_000);
        overall.Samples.Select(x => x.Label).Should().Equal("Tuần 37", "Tuần 38", "Tuần 39");
    }

    [Fact]
    public async Task MonthlySuggestion_IsSkipped_WhenFirstExpenseIsTooRecentForAFullMonth()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 7), 1_000_000, s.Food);

        var result = await s.Service.GetSuggestionsAsync();

        result.ForOverall(BudgetPeriod.Month).Should().BeNull();
        result.ForOverall(BudgetPeriod.Week).Should().NotBeNull();
    }

    [Fact]
    public async Task PerCategorySuggestions_AreSplitByCategory_AndSortedByAverageDescending()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 7), 1_000_000, s.Food);
        await AddAsync(s, new DateOnly(2026, 9, 15), 600_000, s.Food);
        await AddAsync(s, new DateOnly(2026, 9, 16), 500_000, s.Transport);
        await AddAsync(s, new DateOnly(2026, 9, 22), 1_150_000, s.Food);

        var result = await s.Service.GetSuggestionsAsync();

        result.Categories.Select(c => c.CategoryId).Should().Equal(s.Food.Id, s.Transport.Id);
        result.Categories[0].CategoryName.Should().Be(s.Food.Name);

        var food = result.ForCategory(s.Food.Id, BudgetPeriod.Week)!;
        food.Average.Should().Be(916_667);
        food.Suggested.Should().Be(950_000);
        food.Saving.Should().Be(850_000);

        var transport = result.ForCategory(s.Transport.Id, BudgetPeriod.Week)!;
        transport.PeriodsUsed.Should().Be(3); // kỳ không chi vẫn tính là 0
        transport.Average.Should().Be(166_667);
        transport.Suggested.Should().Be(200_000);
        transport.Saving.Should().Be(150_000);
    }

    [Fact]
    public async Task MonthlySuggestion_UsesLastThreeFullMonths()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 6, 1), 1_000_000, s.Food);
        await AddAsync(s, new DateOnly(2026, 7, 20), 2_000_000, s.Food);
        await AddAsync(s, new DateOnly(2026, 8, 31), 3_000_000, s.Food);
        await AddAsync(s, new DateOnly(2026, 9, 5), 7_000_000, s.Food); // tháng hiện tại: bỏ qua

        var result = await s.Service.GetSuggestionsAsync();

        var month = result.ForOverall(BudgetPeriod.Month)!;
        month.Samples.Select(x => x.Label).Should().Equal("T6/2026", "T7/2026", "T8/2026");
        month.Average.Should().Be(2_000_000);
        month.Suggested.Should().Be(2_000_000);
        month.Saving.Should().Be(1_800_000);

        // 3 tuần gần nhất không có khoản chi nào -> không có gợi ý tuần.
        result.ForOverall(BudgetPeriod.Week).Should().BeNull();
    }

    [Fact]
    public async Task OtherUsersExpenses_AreNotCounted()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        await AddAsync(s, new DateOnly(2026, 9, 7), 1_000_000, s.Food);
        await AddAsync(s, new DateOnly(2026, 9, 8), 50_000_000, s.Food, other);

        var result = await s.Service.GetSuggestionsAsync();

        result.ForOverall(BudgetPeriod.Week)!.Samples[0].Amount.Should().Be(1_000_000);
    }
}
