using FluentAssertions;
using Spendly.Web.Common;
using Xunit;

namespace Spendly.Tests;

public class MoneyFormatterTests
{
    [Theory]
    [InlineData(0L, "0 ₫")]
    [InlineData(999L, "999 ₫")]
    [InlineData(1250000L, "1.250.000 ₫")]
    [InlineData(-50000L, "-50.000 ₫")]
    public void Format_ReturnsVietnameseCurrency(long amount, string expected)
    {
        MoneyFormatter.Format(amount).Should().Be(expected);
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(999L, "999")]
    [InlineData(1_000L, "1k")]
    [InlineData(25_000L, "25k")]
    [InlineData(1_500L, "1,5k")]
    [InlineData(999_999L, "1 tr")]
    [InlineData(999_999_999L, "1 tỷ")]
    [InlineData(1_000_000L, "1 tr")]
    [InlineData(2_900_000L, "2,9 tr")]
    [InlineData(3_000_000L, "3 tr")]
    [InlineData(1_500_000_000L, "1,5 tỷ")]
    [InlineData(-25_000L, "-25k")]
    public void FormatCompact_ReturnsShortVietnameseForm(long amount, string expected)
    {
        MoneyFormatter.FormatCompact(amount).Should().Be(expected);
    }
}
