using FluentAssertions;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

/// <summary>Hôm nay trong test: Thứ Ba 29/09/2026 (tuần đã qua 2/7 ngày, còn 6 ngày kể cả hôm nay).</summary>
public class CategoryBudgetEvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private const long Limit = 1_000_000;

    [Theory]
    [InlineData(BudgetPeriod.Week, "Hạn mức tuần · Ăn uống")]
    [InlineData(BudgetPeriod.Month, "Hạn mức tháng · Ăn uống")]
    public void CategoryStatus_LabelNamesTheCategory(BudgetPeriod period, string expected)
    {
        BudgetEvaluator.Evaluate(period, Limit, 0, Today, categoryName: "Ăn uống").PeriodLabel.Should().Be(expected);
    }

    [Fact]
    public void OverallStatus_Wording_IsUnchanged()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, 700_000, 100_000, Today);

        status.PeriodLabel.Should().Be("Hạn mức tuần");
        status.Message.Should().Contain("hạn mức tuần").And.NotContain(" của ");
        status.Advice.Should().NotContain(" cho ");
    }

    [Fact]
    public void Safe_MessageAndAllowanceMentionTheCategory()
    {
        // Còn 600.000 cho 6 ngày = 100.000/ngày.
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, 700_000, 100_000, Today, categoryName: "Ăn uống");

        status.Message.Should().Contain("hạn mức tuần của Ăn uống");
        status.Advice.Should().Contain("100.000 ₫ cho Ăn uống");
    }

    [Fact]
    public void Half_MessageMentionsCategoryLimit()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Month, Limit, 500_000, Today, categoryName: "Mua sắm");

        status.Level.Should().Be(BudgetLevel.Half);
        status.Message.Should().Contain("50%").And.Contain("hạn mức tháng của Mua sắm");
    }

    [Fact]
    public void Exceeded_ReportsOverAmountForTheCategory()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 1_500_000, Today, categoryName: "Ăn uống");

        status.Level.Should().Be(BudgetLevel.Exceeded);
        status.Message.Should().Contain("vượt hạn mức tuần của Ăn uống").And.Contain("500.000 ₫");
        status.Advice.Should().Contain("vẫn có thể ghi chi tiêu bình thường");
    }

    [Fact]
    public void CategoryStatus_UsesSameThresholdsAsOverall()
    {
        foreach (var spent in new long[] { 0, 499_999, 500_000, 750_000, 900_000, 1_000_000, 1_000_001 })
        {
            var overall = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, spent, Today);
            var category = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, spent, Today, categoryName: "Ăn uống");

            category.Level.Should().Be(overall.Level);
            category.Severity.Should().Be(overall.Severity);
        }
    }
}
