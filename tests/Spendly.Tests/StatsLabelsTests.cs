using FluentAssertions;
using Spendly.Web.Models.Stats;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class StatsLabelsTests
{
    [Fact]
    public void RangeLabel_Day()
    {
        var range = new DateRange(new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 29));

        StatsLabels.RangeLabel(StatsPeriod.Day, range).Should().Be("29/09/2026");
    }

    [Fact]
    public void RangeLabel_Week_IncludesIsoWeekNumberAndDates()
    {
        var range = new DateRange(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4));

        StatsLabels.RangeLabel(StatsPeriod.Week, range).Should().Be("Tuần 40, 28/09 – 04/10/2026");
    }

    [Fact]
    public void RangeLabel_Month()
    {
        var range = new DateRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        StatsLabels.RangeLabel(StatsPeriod.Month, range).Should().Be("Tháng 9/2026");
    }

    [Theory]
    [InlineData(StatsPeriod.Week, true, "so với cùng kỳ tuần trước")]
    [InlineData(StatsPeriod.Week, false, "so với tuần trước")]
    [InlineData(StatsPeriod.Month, true, "so với cùng kỳ tháng trước")]
    [InlineData(StatsPeriod.Month, false, "so với tháng trước")]
    [InlineData(StatsPeriod.Day, false, "so với hôm trước")]
    public void ComparisonLabel_DependsOnPeriodAndProgress(StatsPeriod period, bool inProgress, string expected)
    {
        StatsLabels.ComparisonLabel(period, inProgress).Should().Be(expected);
    }

    [Fact]
    public void TrendLabel_WeekUsesVietnameseWeekdays()
    {
        var monday = new DateOnly(2026, 9, 28);

        var labels = Enumerable.Range(0, 7)
            .Select(i => StatsLabels.TrendLabel(StatsPeriod.Week, monday.AddDays(i)))
            .ToArray();

        labels.Should().Equal("T2", "T3", "T4", "T5", "T6", "T7", "CN");
    }

    [Fact]
    public void TrendLabel_MonthUsesDayNumber_DayUsesDayMonth()
    {
        StatsLabels.TrendLabel(StatsPeriod.Month, new DateOnly(2026, 9, 5)).Should().Be("5");
        StatsLabels.TrendLabel(StatsPeriod.Day, new DateOnly(2026, 9, 5)).Should().Be("05/09");
    }
}
