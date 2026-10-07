using FluentAssertions;
using Spendly.Web.Common;
using Spendly.Web.Domain;
using Xunit;

namespace Spendly.Tests;

public class RecurrenceCalculatorTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Occurrence_Weekly_AddsSevenDaysPerStep()
    {
        RecurrenceCalculator.Occurrence(D(2026, 9, 29), RecurrenceFrequency.Weekly, 0).Should().Be(D(2026, 9, 29));
        RecurrenceCalculator.Occurrence(D(2026, 9, 29), RecurrenceFrequency.Weekly, 2).Should().Be(D(2026, 10, 13));
    }

    [Fact]
    public void Occurrence_Monthly_IsAnchoredToStart_NotDriftingAfterShortMonths()
    {
        var start = D(2026, 1, 31);

        RecurrenceCalculator.Occurrence(start, RecurrenceFrequency.Monthly, 1).Should().Be(D(2026, 2, 28));
        RecurrenceCalculator.Occurrence(start, RecurrenceFrequency.Monthly, 2).Should().Be(D(2026, 3, 31));
        RecurrenceCalculator.Occurrence(start, RecurrenceFrequency.Monthly, 3).Should().Be(D(2026, 4, 30));
        RecurrenceCalculator.Occurrence(start, RecurrenceFrequency.Monthly, 13).Should().Be(D(2027, 2, 28));
    }

    [Fact]
    public void Occurrence_Yearly_FromLeapDay_FallsBackToFeb28ThenReturnsToFeb29()
    {
        var start = D(2024, 2, 29);

        RecurrenceCalculator.Occurrence(start, RecurrenceFrequency.Yearly, 1).Should().Be(D(2025, 2, 28));
        RecurrenceCalculator.Occurrence(start, RecurrenceFrequency.Yearly, 4).Should().Be(D(2028, 2, 29));
    }

    [Theory]
    [InlineData(2026, 9, 5, 2026, 9, 5)]    // đúng ngày đến hạn
    [InlineData(2026, 9, 6, 2026, 10, 5)]   // sau ngày đến hạn một ngày
    [InlineData(2026, 10, 5, 2026, 10, 5)]
    [InlineData(2026, 10, 6, 2026, 11, 5)]
    [InlineData(2026, 1, 1, 2026, 9, 5)]    // trước ngày bắt đầu: lần đầu tiên chính là ngày bắt đầu
    public void FirstOnOrAfter_Monthly(int y, int m, int d, int ey, int em, int ed)
    {
        RecurrenceCalculator.FirstOnOrAfter(D(2026, 9, 5), RecurrenceFrequency.Monthly, D(y, m, d)).Should().Be(D(ey, em, ed));
    }

    [Theory]
    [InlineData(2026, 9, 28, 2026, 9, 28)]
    [InlineData(2026, 9, 29, 2026, 10, 5)]
    [InlineData(2026, 10, 5, 2026, 10, 5)]
    [InlineData(2026, 10, 6, 2026, 10, 12)]
    public void FirstOnOrAfter_Weekly(int y, int m, int d, int ey, int em, int ed)
    {
        RecurrenceCalculator.FirstOnOrAfter(D(2026, 9, 28), RecurrenceFrequency.Weekly, D(y, m, d)).Should().Be(D(ey, em, ed));
    }

    [Theory]
    [InlineData(2026, 3, 15, 2026, 3, 15)]
    [InlineData(2026, 3, 16, 2027, 3, 15)]
    public void FirstOnOrAfter_Yearly(int y, int m, int d, int ey, int em, int ed)
    {
        RecurrenceCalculator.FirstOnOrAfter(D(2025, 3, 15), RecurrenceFrequency.Yearly, D(y, m, d)).Should().Be(D(ey, em, ed));
    }

    [Theory]
    [InlineData(2026, 1, 31, 2026, 1, 31)] // ngày cuối tháng 1 -> hạn 28/2
    [InlineData(2026, 2, 1, 2026, 2, 28)]
    [InlineData(2026, 2, 28, 2026, 2, 28)]
    [InlineData(2026, 3, 1, 2026, 3, 31)]
    public void FirstOnOrAfter_Monthly_AtMonthEndAnchor(int y, int m, int d, int ey, int em, int ed)
    {
        // Neo 31/01: 31/01, 28/02, 31/03...
        RecurrenceCalculator.FirstOnOrAfter(D(2026, 1, 31), RecurrenceFrequency.Monthly, D(y, m, d)).Should().Be(D(ey, em, ed));
    }

    [Fact]
    public void NextAfter_ReturnsStrictlyLaterOccurrence()
    {
        RecurrenceCalculator.NextAfter(D(2026, 1, 31), RecurrenceFrequency.Monthly, D(2026, 2, 28)).Should().Be(D(2026, 3, 31));
        RecurrenceCalculator.NextAfter(D(2026, 9, 28), RecurrenceFrequency.Weekly, D(2026, 10, 5)).Should().Be(D(2026, 10, 12));
        RecurrenceCalculator.NextAfter(D(2025, 3, 15), RecurrenceFrequency.Yearly, D(2026, 3, 15)).Should().Be(D(2027, 3, 15));
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Weekly, 2026, 1, 31)]
    [InlineData(RecurrenceFrequency.Monthly, 2026, 1, 31)]
    [InlineData(RecurrenceFrequency.Monthly, 2026, 9, 15)]
    [InlineData(RecurrenceFrequency.Yearly, 2024, 2, 29)]
    public void FirstOnOrAfter_MatchesALinearSearch_ForEveryDayInThreeYears(RecurrenceFrequency frequency, int y, int m, int d)
    {
        var start = D(y, m, d);
        var from = start.AddDays(-40);
        var to = start.AddDays(3 * 366);

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var expected = start;
            var index = 0;
            while (expected < date)
            {
                index++;
                expected = RecurrenceCalculator.Occurrence(start, frequency, index);
            }

            RecurrenceCalculator.FirstOnOrAfter(start, frequency, date).Should().Be(expected, $"date = {date}");
        }
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Weekly, 100_000L, 433_333L)]
    [InlineData(RecurrenceFrequency.Monthly, 3_500_000L, 3_500_000L)]
    [InlineData(RecurrenceFrequency.Yearly, 1_200_000L, 100_000L)]
    [InlineData(RecurrenceFrequency.Yearly, 1_000_000L, 83_333L)]
    public void MonthlyEquivalent_ConvertsToPerMonth(RecurrenceFrequency frequency, long amount, long expected)
    {
        RecurrenceCalculator.MonthlyEquivalent(amount, frequency).Should().Be(expected);
    }
}
