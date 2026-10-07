using FluentAssertions;
using Spendly.Web.Common;
using Spendly.Web.Models.Stats;
using Xunit;

namespace Spendly.Tests;

public class DateRangeCalculatorTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Day_ReturnsSingleDay()
    {
        var range = DateRangeCalculator.GetRange(StatsPeriod.Day, D(2026, 9, 29));

        range.From.Should().Be(D(2026, 9, 29));
        range.To.Should().Be(D(2026, 9, 29));
        range.DayCount.Should().Be(1);
    }

    // 28/09/2026 là Thứ Hai, 04/10/2026 là Chủ Nhật: mọi ngày trong tuần đều cho cùng một khoảng.
    [Theory]
    [InlineData(2026, 9, 28)] // Thứ Hai
    [InlineData(2026, 9, 29)] // Thứ Ba
    [InlineData(2026, 9, 30)]
    [InlineData(2026, 10, 1)]
    [InlineData(2026, 10, 2)]
    [InlineData(2026, 10, 3)] // Thứ Bảy
    [InlineData(2026, 10, 4)] // Chủ Nhật thuộc tuần kết thúc tại chính nó
    public void Week_AnyDayOfWeek_ReturnsMondayToSunday(int y, int m, int d)
    {
        var range = DateRangeCalculator.GetRange(StatsPeriod.Week, D(y, m, d));

        range.From.Should().Be(D(2026, 9, 28));
        range.To.Should().Be(D(2026, 10, 4));
        range.From.DayOfWeek.Should().Be(DayOfWeek.Monday);
        range.To.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        range.DayCount.Should().Be(7);
    }

    [Fact]
    public void Week_CrossingYearBoundary_SpansTwoYears()
    {
        // 31/12/2026 là Thứ Năm.
        var range = DateRangeCalculator.GetRange(StatsPeriod.Week, D(2026, 12, 31));

        range.From.Should().Be(D(2026, 12, 28));
        range.To.Should().Be(D(2027, 1, 3));
    }

    [Theory]
    [InlineData(2026, 9, 29, 2026, 9, 1, 2026, 9, 30)]
    [InlineData(2026, 1, 31, 2026, 1, 1, 2026, 1, 31)]
    [InlineData(2026, 12, 15, 2026, 12, 1, 2026, 12, 31)]
    [InlineData(2026, 2, 10, 2026, 2, 1, 2026, 2, 28)] // năm thường
    [InlineData(2028, 2, 10, 2028, 2, 1, 2028, 2, 29)] // năm nhuận
    public void Month_ReturnsFirstToLastDayOfMonth(int y, int m, int d, int fy, int fm, int fd, int ty, int tm, int td)
    {
        var range = DateRangeCalculator.GetRange(StatsPeriod.Month, D(y, m, d));

        range.From.Should().Be(D(fy, fm, fd));
        range.To.Should().Be(D(ty, tm, td));
    }

    [Fact]
    public void Previous_Day_IsYesterday()
    {
        var previous = DateRangeCalculator.GetPrevious(StatsPeriod.Day, D(2026, 3, 1));

        previous.From.Should().Be(D(2026, 2, 28));
        previous.To.Should().Be(D(2026, 2, 28));
    }

    [Fact]
    public void Previous_Week_IsPreviousMondayToSunday()
    {
        var previous = DateRangeCalculator.GetPrevious(StatsPeriod.Week, D(2026, 9, 29));

        previous.From.Should().Be(D(2026, 9, 21));
        previous.To.Should().Be(D(2026, 9, 27));
    }

    [Fact]
    public void Previous_Month_OfJanuary_IsDecemberOfPreviousYear()
    {
        var previous = DateRangeCalculator.GetPrevious(StatsPeriod.Month, D(2027, 1, 15));

        previous.From.Should().Be(D(2026, 12, 1));
        previous.To.Should().Be(D(2026, 12, 31));
    }

    [Fact]
    public void Previous_Month_FromThe31st_LandsInShorterMonth()
    {
        var previous = DateRangeCalculator.GetPrevious(StatsPeriod.Month, D(2026, 3, 31));

        previous.From.Should().Be(D(2026, 2, 1));
        previous.To.Should().Be(D(2026, 2, 28));
    }

    [Theory]
    [InlineData(StatsPeriod.Day, 1, 2026, 9, 30)]
    [InlineData(StatsPeriod.Week, 1, 2026, 10, 6)]
    [InlineData(StatsPeriod.Month, 1, 2026, 10, 29)]
    [InlineData(StatsPeriod.Day, -1, 2026, 9, 28)]
    [InlineData(StatsPeriod.Week, -1, 2026, 9, 22)]
    [InlineData(StatsPeriod.Month, -1, 2026, 8, 29)]
    public void Shift_MovesAnchorByWholePeriods(StatsPeriod period, int steps, int y, int m, int d)
    {
        DateRangeCalculator.Shift(period, D(2026, 9, 29), steps).Should().Be(D(y, m, d));
    }

    [Fact]
    public void Contains_IncludesBothEnds()
    {
        var range = new DateRange(D(2026, 9, 28), D(2026, 10, 4));

        range.Contains(D(2026, 9, 28)).Should().BeTrue();
        range.Contains(D(2026, 10, 4)).Should().BeTrue();
        range.Contains(D(2026, 9, 27)).Should().BeFalse();
        range.Contains(D(2026, 10, 5)).Should().BeFalse();
    }
}
