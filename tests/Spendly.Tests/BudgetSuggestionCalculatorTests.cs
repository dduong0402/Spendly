using FluentAssertions;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Models.Stats;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

/// <summary>Hôm nay: Thứ Ba 29/09/2026. Tuần hiện tại 28/09 - 04/10; ba tuần trọn vẹn trước đó bắt đầu 07/09, 14/09, 21/09.</summary>
public class BudgetSuggestionCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    private static SuggestionSample[] Samples(params long[] amounts) =>
        amounts.Select((a, i) => new SuggestionSample($"K{i + 1}", a)).ToArray();

    // ---------- Khoảng lịch sử ----------

    [Fact]
    public void GetHistoryRanges_Week_ReturnsThreeFullWeeksOldestFirst_ExcludingCurrent()
    {
        var ranges = BudgetSuggestionCalculator.GetHistoryRanges(BudgetPeriod.Week, Today, new DateOnly(2026, 1, 1));

        ranges.Should().Equal(
            new DateRange(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13)),
            new DateRange(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20)),
            new DateRange(new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27)));
    }

    [Fact]
    public void GetHistoryRanges_Month_ReturnsThreeFullMonths_ExcludingCurrent()
    {
        var ranges = BudgetSuggestionCalculator.GetHistoryRanges(BudgetPeriod.Month, Today, new DateOnly(2026, 1, 1));

        ranges.Select(r => r.From).Should().Equal(new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1));
        ranges[2].To.Should().Be(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void GetHistoryRanges_SkipsPeriodsThatStartedBeforeFirstExpense()
    {
        // Khoản chi đầu tiên 10/09 (giữa tuần 07/09 - 13/09) -> tuần đó bị bỏ vì chưa trọn vẹn dữ liệu.
        var ranges = BudgetSuggestionCalculator.GetHistoryRanges(BudgetPeriod.Week, Today, new DateOnly(2026, 9, 10));

        ranges.Select(r => r.From).Should().Equal(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21));
    }

    [Fact]
    public void GetHistoryRanges_FirstExpenseOnFirstDayOfPeriod_KeepsThatPeriod()
    {
        var ranges = BudgetSuggestionCalculator.GetHistoryRanges(BudgetPeriod.Week, Today, new DateOnly(2026, 9, 14));

        ranges.Select(r => r.From).Should().Equal(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21));
    }

    [Fact]
    public void GetHistoryRanges_WithoutExpenses_OrOnlyRecentOnes_IsEmpty()
    {
        BudgetSuggestionCalculator.GetHistoryRanges(BudgetPeriod.Week, Today, null).Should().BeEmpty();
        BudgetSuggestionCalculator.GetHistoryRanges(BudgetPeriod.Month, Today, new DateOnly(2026, 9, 15)).Should().BeEmpty();
    }

    [Fact]
    public void GetHistoryRanges_Month_AtMonthEnd_DoesNotSkipOrRepeatMonths()
    {
        // 31/03: lùi 1 tháng rơi vào 28/02 (tháng ngắn) - vẫn phải ra Dec, Jan, Feb liên tiếp.
        var ranges = BudgetSuggestionCalculator.GetHistoryRanges(BudgetPeriod.Month, new DateOnly(2026, 3, 31), new DateOnly(2025, 1, 1));

        ranges.Select(r => r.From).Should().Equal(new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1));
    }

    [Fact]
    public void SampleLabel_UsesIsoWeekAndMonthYear()
    {
        BudgetSuggestionCalculator.SampleLabel(BudgetPeriod.Week, new DateRange(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13))).Should().Be("Tuần 37");
        BudgetSuggestionCalculator.SampleLabel(BudgetPeriod.Month, new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31))).Should().Be("T8/2026");
    }

    // ---------- Tính gợi ý ----------

    [Fact]
    public void Build_RoundsAverageUpToNiceStep_AndOffersSaving()
    {
        var s = BudgetSuggestionCalculator.Build(BudgetPeriod.Week, Samples(1_000_000, 1_100_000, 1_150_000))!;

        s.PeriodsUsed.Should().Be(3);
        s.Average.Should().Be(1_083_333);
        s.Suggested.Should().Be(1_100_000);
        s.Saving.Should().Be(1_000_000);
        s.Period.Should().Be(BudgetPeriod.Week);
    }

    [Fact]
    public void Build_AverageAlreadyRound_KeepsItAsSuggested()
    {
        var s = BudgetSuggestionCalculator.Build(BudgetPeriod.Month, Samples(2_900_000, 3_000_000, 2_800_000))!;

        s.Average.Should().Be(2_900_000);
        s.Suggested.Should().Be(2_900_000);
        s.Saving.Should().Be(2_600_000);
    }

    [Fact]
    public void Build_SavingIsOmitted_WhenItWouldRoundToZeroOrNotBeLower()
    {
        var tiny = BudgetSuggestionCalculator.Build(BudgetPeriod.Week, Samples(5_000))!;
        tiny.Suggested.Should().Be(10_000);
        tiny.Saving.Should().BeNull();
    }

    [Fact]
    public void Build_PeriodsWithZeroSpending_CountTowardTheAverage()
    {
        var s = BudgetSuggestionCalculator.Build(BudgetPeriod.Week, Samples(0, 0, 600_000))!;

        s.PeriodsUsed.Should().Be(3);
        s.Average.Should().Be(200_000);
    }

    [Fact]
    public void Build_WithNoSamplesOrNoSpending_ReturnsNull()
    {
        BudgetSuggestionCalculator.Build(BudgetPeriod.Week, Array.Empty<SuggestionSample>()).Should().BeNull();
        BudgetSuggestionCalculator.Build(BudgetPeriod.Week, Samples(0, 0, 0)).Should().BeNull();
    }

    [Fact]
    public void Build_IsCappedAtMaxAmount()
    {
        var s = BudgetSuggestionCalculator.Build(BudgetPeriod.Month, Samples(ExpenseRules.MaxAmount))!;

        s.Suggested.Should().Be(ExpenseRules.MaxAmount);
        s.Suggested.Should().BeLessThanOrEqualTo(ExpenseRules.MaxAmount);
    }

    [Theory]
    [InlineData(1L, 10_000L)]
    [InlineData(99_999L, 10_000L)]
    [InlineData(100_000L, 50_000L)]
    [InlineData(999_999L, 50_000L)]
    [InlineData(1_000_000L, 100_000L)]
    [InlineData(9_999_999L, 100_000L)]
    [InlineData(10_000_000L, 500_000L)]
    public void NiceStep_GrowsWithTheAmount(long amount, long expected)
    {
        BudgetSuggestionCalculator.NiceStep(amount).Should().Be(expected);
    }
}
