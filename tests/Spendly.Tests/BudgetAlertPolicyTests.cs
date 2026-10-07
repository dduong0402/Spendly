using FluentAssertions;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class BudgetAlertPolicyTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private const long Limit = 1_000_000;

    private static BudgetStatus Week(long spent) => BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, spent, Today);

    private static BudgetStatus Month(long spent) => BudgetEvaluator.Evaluate(BudgetPeriod.Month, Limit, spent, Today);

    private static IReadOnlyList<BudgetStatus> One(BudgetStatus status) => new[] { status };

    [Fact]
    public void CrossingHalf_RaisesInfoAlert()
    {
        var alerts = BudgetAlertPolicy.Detect(One(Week(400_000)), One(Week(500_000)));

        var alert = alerts.Should().ContainSingle().Subject;
        alert.Severity.Should().Be("info");
        alert.Period.Should().Be("Week");
        alert.Title.Should().Contain("Hạn mức tuần").And.Contain("một nửa");
        alert.Advice.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(600_000L, 700_000L)]   // vẫn trong mốc 50%
    [InlineData(100_000L, 200_000L)]   // vẫn an toàn
    [InlineData(800_000L, 850_000L)]   // vẫn trong mốc 75%
    public void StayingInTheSameLevel_RaisesNoAlert(long before, long after)
    {
        BudgetAlertPolicy.Detect(One(Week(before)), One(Week(after))).Should().BeEmpty();
    }

    [Fact]
    public void JumpingSeveralLevels_RaisesOneAlertForTheHighestLevel()
    {
        var alerts = BudgetAlertPolicy.Detect(One(Week(400_000)), One(Week(950_000)));

        var alert = alerts.Should().ContainSingle().Subject;
        alert.Title.Should().Contain("Sắp chạm");
        alert.Severity.Should().Be("warning");
    }

    [Fact]
    public void ReachingTheLimit_RaisesDangerAlert()
    {
        var alerts = BudgetAlertPolicy.Detect(One(Week(950_000)), One(Week(1_000_000)));

        var alert = alerts.Should().ContainSingle().Subject;
        alert.Severity.Should().Be("danger");
        alert.Title.Should().Contain("Đã chạm hạn mức");
    }

    [Fact]
    public void WhenAlreadyExceeded_EachExpenseThatAddsSpendingRaisesExceededNotice()
    {
        var alerts = BudgetAlertPolicy.Detect(One(Week(1_200_000)), One(Week(1_300_000)));

        var alert = alerts.Should().ContainSingle().Subject;
        alert.Severity.Should().Be("danger");
        alert.Title.Should().Contain("Đã vượt hạn mức");
        alert.Message.Should().Contain("300.000 ₫");
    }

    [Fact]
    public void WhenExceeded_ButSpendingUnchangedOrReduced_RaisesNoAlert()
    {
        BudgetAlertPolicy.Detect(One(Week(1_300_000)), One(Week(1_300_000))).Should().BeEmpty();
        BudgetAlertPolicy.Detect(One(Week(1_300_000)), One(Week(1_200_000))).Should().BeEmpty();
    }

    [Fact]
    public void LevelGoingDown_RaisesNoAlert()
    {
        BudgetAlertPolicy.Detect(One(Week(950_000)), One(Week(600_000))).Should().BeEmpty();
    }

    [Fact]
    public void ExpenseOutsideCurrentPeriod_LeavesStatusUnchanged_SoNoAlert()
    {
        BudgetAlertPolicy.Detect(One(Week(950_000)), One(Week(950_000))).Should().BeEmpty();
    }

    [Fact]
    public void TwoBudgets_AreEvaluatedIndependently()
    {
        var before = new[] { Week(400_000), Month(100_000) };
        var after = new[] { Week(600_000), Month(150_000) };

        var alerts = BudgetAlertPolicy.Detect(before, after);

        alerts.Should().ContainSingle().Which.Period.Should().Be("Week");
    }

    [Fact]
    public void BothBudgetsCrossing_RaisesTwoAlerts()
    {
        var before = new[] { Week(400_000), Month(400_000) };
        var after = new[] { Week(600_000), Month(950_000) };

        BudgetAlertPolicy.Detect(before, after).Should().HaveCount(2);
    }

    [Fact]
    public void BudgetThatDidNotExistBefore_IsIgnored()
    {
        BudgetAlertPolicy.Detect(Array.Empty<BudgetStatus>(), One(Week(900_000))).Should().BeEmpty();
    }
}
