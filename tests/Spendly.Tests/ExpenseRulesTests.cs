using FluentAssertions;
using Spendly.Web.Models.Expenses;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class ExpenseRulesTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    private static SaveExpenseRequest Request(long amount = 50_000, DateOnly? spentAt = null, string? note = null) =>
        new(amount, 1, spentAt ?? Today, note);

    [Fact]
    public void Validate_ValidRequest_ReturnsNull()
    {
        ExpenseRules.Validate(Request(), Today).Should().BeNull();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(ExpenseRules.MaxAmount + 1)]
    public void Validate_AmountOutOfRange_ReturnsError(long amount)
    {
        ExpenseRules.Validate(Request(amount), Today).Should().NotBeNull();
    }

    [Fact]
    public void Validate_MaxAmount_IsAllowed()
    {
        ExpenseRules.Validate(Request(ExpenseRules.MaxAmount), Today).Should().BeNull();
    }

    [Fact]
    public void Validate_FutureDate_ReturnsError()
    {
        ExpenseRules.Validate(Request(spentAt: Today.AddDays(1)), Today).Should().NotBeNull();
    }

    [Fact]
    public void Validate_DateBeforeMinDate_ReturnsError()
    {
        ExpenseRules.Validate(Request(spentAt: new DateOnly(1999, 12, 31)), Today).Should().NotBeNull();
    }

    [Fact]
    public void Validate_NoteTooLong_ReturnsError()
    {
        ExpenseRules.Validate(Request(note: new string('a', ExpenseRules.MaxNoteLength + 1)), Today).Should().NotBeNull();
    }
}
