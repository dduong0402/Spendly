using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Data.Seed;
using Spendly.Web.Domain;
using Spendly.Web.Models.Stats;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

/// <summary>
/// Đồng hồ test mặc định: 29/09/2026 10:00 giờ Việt Nam = Thứ Ba.
/// Tuần hiện tại: Thứ Hai 28/09 - Chủ Nhật 04/10. Tuần trước: 21/09 - 27/09.
/// </summary>
public class StatsServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    private sealed record Setup(
        TestDb Db,
        StatsService Service,
        ApplicationUser User,
        Category Food,
        Category Transport);

    private static async Task<Setup> ArrangeAsync()
    {
        var t = TestDb.Create();
        await DbSeeder.SeedAsync(t.Db);
        var user = await t.AddUserAsync("a@example.com");
        var categories = await t.Db.Categories.Where(c => c.UserId == null).OrderBy(c => c.Id).Take(2).ToListAsync();
        var service = new StatsService(t.Db, new FakeCurrentUser { UserId = user.Id }, t.Clock);
        return new Setup(t, service, user, categories[0], categories[1]);
    }

    private static async Task AddAsync(Setup s, DateOnly date, long amount, Category? category = null, string? note = null, ApplicationUser? user = null)
    {
        s.Db.Db.Expenses.Add(new Expense
        {
            UserId = (user ?? s.User).Id,
            CategoryId = (category ?? s.Food).Id,
            Amount = amount,
            SpentAt = date,
            Note = note
        });
        await s.Db.Db.SaveChangesAsync();
    }

    // ---------- Summary ----------

    [Fact]
    public async Task Summary_Day_CountsOnlyThatDay()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, Today, 40_000);
        await AddAsync(s, Today, 60_000);
        await AddAsync(s, Today.AddDays(-1), 999_000);

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Day, Today);

        summary.Period.Should().Be("day");
        summary.TotalAmount.Should().Be(100_000);
        summary.TransactionCount.Should().Be(2);
        summary.AveragePerDay.Should().Be(100_000);
        summary.PreviousTotal.Should().Be(999_000);
        summary.IsCurrent.Should().BeTrue();
        summary.RangeLabel.Should().Be("29/09/2026");
    }

    [Fact]
    public async Task Summary_Week_SumsMondayToSundayAndIgnoresOtherWeeks()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 27), 30_000); // Chủ Nhật tuần trước
        await AddAsync(s, new DateOnly(2026, 9, 28), 100_000); // Thứ Hai
        await AddAsync(s, new DateOnly(2026, 9, 29), 50_000);
        await AddAsync(s, new DateOnly(2026, 10, 5), 70_000); // Thứ Hai tuần sau

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Week, Today);

        summary.From.Should().Be(new DateOnly(2026, 9, 28));
        summary.To.Should().Be(new DateOnly(2026, 10, 4));
        summary.TotalAmount.Should().Be(150_000);
        summary.TransactionCount.Should().Be(2);
        summary.RangeLabel.Should().Be("Tuần 40, 28/09 – 04/10/2026");
    }

    [Fact]
    public async Task Summary_CurrentWeek_AveragesOverElapsedDaysOnly()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 28), 100_000);
        await AddAsync(s, new DateOnly(2026, 9, 29), 50_000);

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Week, Today);

        // Đã trôi qua 2 ngày (Thứ Hai, Thứ Ba): 150.000 / 2.
        summary.AveragePerDay.Should().Be(75_000);
    }

    [Fact]
    public async Task Summary_PastMonth_AveragesOverFullMonthLength()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 8, 10), 310_000);

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Month, new DateOnly(2026, 8, 15));

        // Tháng 8 có 31 ngày.
        summary.AveragePerDay.Should().Be(10_000);
        summary.IsCurrent.Should().BeFalse();
        summary.HasNext.Should().BeTrue();
    }

    [Fact]
    public async Task Summary_FuturePeriod_HasZeroAverageAndNoChange()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, Today, 50_000);

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Week, Today.AddDays(14));

        summary.TotalAmount.Should().Be(0);
        summary.AveragePerDay.Should().Be(0);
        summary.ChangePercent.Should().BeNull();
        summary.HasNext.Should().BeFalse();
    }

    [Fact]
    public async Task Summary_CurrentWeek_ComparesWithSameNumberOfDaysOfPreviousWeek()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        // Tuần này (đã qua 2 ngày): 150.000.
        await AddAsync(s, new DateOnly(2026, 9, 28), 100_000);
        await AddAsync(s, new DateOnly(2026, 9, 29), 50_000);
        // Tuần trước: chỉ 2 ngày đầu (Thứ Hai, Thứ Ba) được so sánh = 100.000; Thứ Tư 500.000 bị bỏ qua.
        await AddAsync(s, new DateOnly(2026, 9, 21), 50_000);
        await AddAsync(s, new DateOnly(2026, 9, 22), 50_000);
        await AddAsync(s, new DateOnly(2026, 9, 23), 500_000);

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Week, Today);

        summary.PreviousTotal.Should().Be(100_000);
        summary.ChangePercent.Should().Be(50.0);
        summary.ComparisonLabel.Should().Be("so với cùng kỳ tuần trước");
    }

    [Fact]
    public async Task Summary_FinishedWeek_ComparesWithWholePreviousWeek()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 22), 200_000); // tuần 21-27/09
        await AddAsync(s, new DateOnly(2026, 9, 15), 100_000); // tuần 14-20/09
        await AddAsync(s, new DateOnly(2026, 9, 17), 100_000);

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Week, new DateOnly(2026, 9, 23));

        summary.TotalAmount.Should().Be(200_000);
        summary.PreviousTotal.Should().Be(200_000);
        summary.ChangePercent.Should().Be(0.0);
        summary.ComparisonLabel.Should().Be("so với tuần trước");
    }

    [Fact]
    public async Task Summary_WhenPreviousIsZero_ChangePercentIsNull()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, Today, 50_000);

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Week, Today);

        summary.PreviousTotal.Should().Be(0);
        summary.ChangePercent.Should().BeNull();
    }

    [Fact]
    public async Task Summary_ChangePercent_IsRoundedToOneDecimal()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 8, 5), 300_000); // tháng 8
        await AddAsync(s, new DateOnly(2026, 9, 5), 400_000); // tháng 9 (đã qua 29/30 ngày)

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Month, Today);

        // Tháng 8 chỉ tính 29 ngày đầu, ngày 5/8 nằm trong đó: (400.000 - 300.000) / 300.000 = 33,3%.
        summary.ChangePercent.Should().Be(33.3);
    }

    [Fact]
    public async Task Summary_Navigation_AnchorsAndHasNext()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var current = await s.Service.GetSummaryAsync(StatsPeriod.Week, Today);
        var lastWeek = await s.Service.GetSummaryAsync(StatsPeriod.Week, current.PreviousDate);

        current.PreviousDate.Should().Be(new DateOnly(2026, 9, 22));
        current.NextDate.Should().Be(new DateOnly(2026, 10, 6));
        current.HasNext.Should().BeFalse();
        lastWeek.HasNext.Should().BeTrue();
        lastWeek.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task Summary_OnlyIncludesCurrentUsersExpenses()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        await AddAsync(s, Today, 50_000);
        await AddAsync(s, Today, 900_000, user: other);

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Day, Today);

        summary.TotalAmount.Should().Be(50_000);
        summary.TransactionCount.Should().Be(1);
    }

    [Fact]
    public async Task Summary_WithNoData_ReturnsZeros()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var summary = await s.Service.GetSummaryAsync(StatsPeriod.Month, Today);

        summary.TotalAmount.Should().Be(0);
        summary.TransactionCount.Should().Be(0);
        summary.AveragePerDay.Should().Be(0);
        summary.ChangePercent.Should().BeNull();
    }

    // ---------- Trend ----------

    [Fact]
    public async Task Trend_Week_ReturnsSevenDaysWithZerosAndFutureFlags()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 28), 100_000);
        await AddAsync(s, new DateOnly(2026, 9, 28), 20_000);
        await AddAsync(s, new DateOnly(2026, 9, 29), 50_000);

        var points = await s.Service.GetTrendAsync(StatsPeriod.Week, Today);

        points.Should().HaveCount(7);
        points.Select(p => p.Label).Should().Equal("T2", "T3", "T4", "T5", "T6", "T7", "CN");
        points.Select(p => p.Amount).Should().Equal(120_000, 50_000, 0, 0, 0, 0, 0);
        points.Select(p => p.IsFuture).Should().Equal(false, false, true, true, true, true, true);
        points.First().Date.Should().Be(new DateOnly(2026, 9, 28));
        points.Last().Date.Should().Be(new DateOnly(2026, 10, 4));
    }

    [Fact]
    public async Task Trend_Month_ReturnsEveryDayOfMonth()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 1), 10_000);
        await AddAsync(s, new DateOnly(2026, 9, 30), 20_000);

        var points = await s.Service.GetTrendAsync(StatsPeriod.Month, Today);

        points.Should().HaveCount(30);
        points.First().Label.Should().Be("1");
        points.Last().Label.Should().Be("30");
        points.First().Amount.Should().Be(10_000);
        points.Last().Amount.Should().Be(20_000);
        points.Count(p => p.IsFuture).Should().Be(1); // chỉ ngày 30/09 chưa tới
    }

    [Fact]
    public async Task Trend_Day_ReturnsSevenDaysEndingOnThatDay()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 23), 10_000);
        await AddAsync(s, new DateOnly(2026, 9, 29), 30_000);
        await AddAsync(s, new DateOnly(2026, 9, 22), 999_000); // ngoài 7 ngày

        var points = await s.Service.GetTrendAsync(StatsPeriod.Day, Today);

        points.Should().HaveCount(7);
        points.First().Date.Should().Be(new DateOnly(2026, 9, 23));
        points.Last().Date.Should().Be(Today);
        points.First().Label.Should().Be("23/09");
        points.Sum(p => p.Amount).Should().Be(40_000);
        points.Should().OnlyContain(p => !p.IsFuture);
    }

    [Fact]
    public async Task Trend_WithNoData_StillReturnsZeroPoints()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var points = await s.Service.GetTrendAsync(StatsPeriod.Week, Today);

        points.Should().HaveCount(7);
        points.Should().OnlyContain(p => p.Amount == 0);
    }

    // ---------- By category ----------

    [Fact]
    public async Task ByCategory_GroupsSortsByAmountAndComputesPercent()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, Today, 100_000, s.Food);
        await AddAsync(s, Today, 200_000, s.Transport);
        await AddAsync(s, new DateOnly(2026, 9, 28), 100_000, s.Transport);

        var shares = await s.Service.GetByCategoryAsync(StatsPeriod.Week, Today);

        shares.Should().HaveCount(2);
        shares[0].Name.Should().Be(s.Transport.Name);
        shares[0].Amount.Should().Be(300_000);
        shares[0].Count.Should().Be(2);
        shares[0].Percent.Should().Be(75.0);
        shares[1].Name.Should().Be(s.Food.Name);
        shares[1].Percent.Should().Be(25.0);
        shares[0].Emoji.Should().NotBeNullOrEmpty();
        shares[0].Color.Should().StartWith("#");
    }

    [Fact]
    public async Task ByCategory_EmptyPeriod_ReturnsEmptyList()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var shares = await s.Service.GetByCategoryAsync(StatsPeriod.Week, Today);

        shares.Should().BeEmpty();
    }

    [Fact]
    public async Task ByCategory_PercentagesRoundToOneDecimal()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, Today, 100_000, s.Food);
        await AddAsync(s, Today, 200_000, s.Transport);

        var shares = await s.Service.GetByCategoryAsync(StatsPeriod.Day, Today);

        shares.Select(x => x.Percent).Should().Equal(66.7, 33.3);
    }

    // ---------- Top / Recent ----------

    [Fact]
    public async Task TopExpenses_ReturnsLargestFirstLimitedByTake()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, Today, 10_000, note: "nhỏ");
        await AddAsync(s, Today, 300_000, note: "lớn nhất");
        await AddAsync(s, Today, 50_000, note: "vừa");
        await AddAsync(s, new DateOnly(2026, 9, 1), 900_000, note: "ngoài kỳ");

        var top = await s.Service.GetTopExpensesAsync(StatsPeriod.Week, Today, take: 2);

        top.Select(x => x.Note).Should().Equal("lớn nhất", "vừa");
    }

    [Fact]
    public async Task RecentExpenses_ReturnsNewestFirst_AndIncludesCategoryInfo()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddAsync(s, new DateOnly(2026, 9, 28), 10_000, note: "cũ");
        await AddAsync(s, Today, 20_000, s.Transport, note: "mới");

        var recent = await s.Service.GetRecentExpensesAsync(StatsPeriod.Week, Today);

        recent.Select(x => x.Note).Should().Equal("mới", "cũ");
        recent[0].CategoryName.Should().Be(s.Transport.Name);
        recent[0].CategoryEmoji.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1000, 20)]
    public async Task TopExpenses_ClampsTake(int take, int expectedMaxCount)
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        for (var i = 1; i <= 25; i++)
        {
            await AddAsync(s, Today, i * 1_000);
        }

        var top = await s.Service.GetTopExpensesAsync(StatsPeriod.Day, Today, take);

        top.Should().HaveCount(expectedMaxCount);
    }
}
