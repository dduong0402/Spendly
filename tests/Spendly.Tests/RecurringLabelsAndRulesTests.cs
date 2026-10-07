using FluentAssertions;
using Spendly.Web.Domain;
using Spendly.Web.Models.Recurring;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class RecurringLabelsTests
{
    [Fact]
    public void Frequency_ReturnsVietnameseLabels()
    {
        RecurringLabels.Frequency(RecurrenceFrequency.Weekly).Should().Be("Hằng tuần");
        RecurringLabels.Frequency(RecurrenceFrequency.Monthly).Should().Be("Hằng tháng");
        RecurringLabels.Frequency(RecurrenceFrequency.Yearly).Should().Be("Hằng năm");
    }

    [Fact]
    public void Schedule_DescribesTheAnchorDay()
    {
        RecurringLabels.Schedule(RecurrenceFrequency.Weekly, new DateOnly(2026, 9, 28)).Should().Be("Mỗi Thứ Hai");
        RecurringLabels.Schedule(RecurrenceFrequency.Monthly, new DateOnly(2026, 9, 5)).Should().Be("Ngày 5 hằng tháng");
        RecurringLabels.Schedule(RecurrenceFrequency.Yearly, new DateOnly(2026, 3, 5)).Should().Be("Ngày 05/03 hằng năm");
    }

    [Fact]
    public void Schedule_MonthlyOnDayAbove28_MentionsMonthEnd()
    {
        RecurringLabels.Schedule(RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 31)).Should().Contain("cuối tháng");
    }
}

public class RecurringExpenseRulesTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    private static SaveRecurringRequest Valid(Func<SaveRecurringRequest, SaveRecurringRequest>? change = null)
    {
        var request = new SaveRecurringRequest("Tiền nhà", 3_500_000, 1, RecurrenceFrequency.Monthly, Today, null);
        return change is null ? request : change(request);
    }

    [Fact]
    public void Validate_ValidRequest_ReturnsNull()
    {
        RecurringExpenseRules.Validate(Valid(), Today).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_IsRejected(string name)
    {
        RecurringExpenseRules.Validate(Valid(r => r with { Name = name }), Today).Should().NotBeNull();
    }

    [Fact]
    public void Validate_NameLongerThanMax_IsRejected()
    {
        RecurringExpenseRules.Validate(Valid(r => r with { Name = new string('a', 101) }), Today).Should().NotBeNull();
        RecurringExpenseRules.Validate(Valid(r => r with { Name = new string('a', 100) }), Today).Should().BeNull();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(ExpenseRules.MaxAmount + 1)]
    public void Validate_InvalidAmount_IsRejected(long amount)
    {
        RecurringExpenseRules.Validate(Valid(r => r with { Amount = amount }), Today).Should().NotBeNull();
    }

    [Fact]
    public void Validate_UnknownFrequency_IsRejected()
    {
        RecurringExpenseRules.Validate(Valid(r => r with { Frequency = (RecurrenceFrequency)99 }), Today).Should().NotBeNull();
    }

    [Fact]
    public void Validate_StartDateWindow_IsOneYearEitherWay()
    {
        RecurringExpenseRules.Validate(Valid(r => r with { StartDate = Today.AddDays(-366) }), Today).Should().BeNull();
        RecurringExpenseRules.Validate(Valid(r => r with { StartDate = Today.AddDays(-367) }), Today).Should().NotBeNull();
        RecurringExpenseRules.Validate(Valid(r => r with { StartDate = Today.AddDays(366) }), Today).Should().BeNull();
        RecurringExpenseRules.Validate(Valid(r => r with { StartDate = Today.AddDays(367) }), Today).Should().NotBeNull();
    }

    [Fact]
    public void Validate_EndDateBeforeStart_IsRejected_ButSameDayIsAllowed()
    {
        RecurringExpenseRules.Validate(Valid(r => r with { EndDate = Today.AddDays(-1) }), Today).Should().NotBeNull();
        RecurringExpenseRules.Validate(Valid(r => r with { EndDate = Today }), Today).Should().BeNull();
    }

    [Fact]
    public void ValidateUpdate_ChecksNameAmountAndEndAgainstExistingStart()
    {
        var start = new DateOnly(2026, 6, 5);

        RecurringExpenseRules.Validate(new UpdateRecurringRequest("Netflix", 260_000, 1, null), start).Should().BeNull();
        RecurringExpenseRules.Validate(new UpdateRecurringRequest("", 260_000, 1, null), start).Should().NotBeNull();
        RecurringExpenseRules.Validate(new UpdateRecurringRequest("Netflix", 0, 1, null), start).Should().NotBeNull();
        RecurringExpenseRules.Validate(new UpdateRecurringRequest("Netflix", 260_000, 1, start.AddDays(-1)), start).Should().NotBeNull();
    }
}
