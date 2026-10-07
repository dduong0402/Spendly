using FluentAssertions;
using Spendly.Web.Common;
using Xunit;

namespace Spendly.Tests;

public class MoneyParserTests
{
    [Theory]
    [InlineData("50000", 50000L)]
    [InlineData("50.000", 50000L)]
    [InlineData("1.250.000", 1250000L)]
    [InlineData("1,250,000", 1250000L)]
    [InlineData("1 250 000", 1250000L)]
    [InlineData("1.250.000 ₫", 1250000L)]
    [InlineData("  75.000đ ", 75000L)]
    [InlineData("0", 0L)]
    public void TryParse_ValidInput_ReturnsAmount(string input, long expected)
    {
        MoneyParser.TryParse(input, out var amount).Should().BeTrue();
        amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("-5000")]
    [InlineData("1,5")]
    [InlineData("1.25")]
    [InlineData("12.34.567")]
    [InlineData("50.000,50")]
    [InlineData("99999999999999999999")]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        MoneyParser.TryParse(input, out _).Should().BeFalse();
    }

    [Fact]
    public void FormatNumber_UsesDotSeparators()
    {
        MoneyFormatter.FormatNumber(1250000).Should().Be("1.250.000");
    }
}
