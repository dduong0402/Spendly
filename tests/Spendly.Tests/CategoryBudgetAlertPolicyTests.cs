using FluentAssertions;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class CategoryBudgetAlertPolicyTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private const long Limit = 1_000_000;

    private static BudgetStatus Overall(BudgetPeriod period, long spent) =>
        BudgetEvaluator.Evaluate(period, Limit, spent, Today);

    private static CategoryBudgetStatus Cat(int id, string name, BudgetPeriod period, long spent) =>
        new(BudgetEvaluator.Evaluate(period, Limit, spent, Today, categoryName: name), id, name, "#1F6BFF", "🍽️");

    private static BudgetSnapshot Snapshot(IEnumerable<BudgetStatus>? overall = null, IEnumerable<CategoryBudgetStatus>? categories = null) =>
        new((overall ?? Array.Empty<BudgetStatus>()).ToList(), (categories ?? Array.Empty<CategoryBudgetStatus>()).ToList());

    [Fact]
    public void CategoryCrossingAThreshold_RaisesAlertNamingTheCategory()
    {
        var before = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Month, 400_000) });
        var after = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Month, 600_000) });

        var alerts = BudgetAlertPolicy.Detect(before, after);

        var alert = alerts.Should().ContainSingle().Subject;
        alert.Title.Should().Contain("Hạn mức tháng · Ăn uống").And.Contain("một nửa");
        alert.Severity.Should().Be("info");
        alert.Period.Should().Be("Month");
    }

    [Fact]
    public void UnrelatedCategoryUnchanged_RaisesNoAlert()
    {
        var before = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Month, 900_000), Cat(2, "Di chuyển", BudgetPeriod.Month, 100_000) });
        var after = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Month, 900_000), Cat(2, "Di chuyển", BudgetPeriod.Month, 100_000) });

        BudgetAlertPolicy.Detect(before, after).Should().BeEmpty();
    }

    [Fact]
    public void SameCategoryButDifferentPeriods_AreIndependent()
    {
        var before = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 100_000), Cat(1, "Ăn uống", BudgetPeriod.Month, 100_000) });
        var after = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 600_000), Cat(1, "Ăn uống", BudgetPeriod.Month, 100_000) });

        var alerts = BudgetAlertPolicy.Detect(before, after);

        alerts.Should().ContainSingle().Which.Period.Should().Be("Week");
    }

    [Fact]
    public void CategoryAlreadyExceeded_AndSpendingMore_RaisesNoticeEachTime()
    {
        var before = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 1_200_000) });
        var after = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 1_250_000) });

        var alert = BudgetAlertPolicy.Detect(before, after).Should().ContainSingle().Subject;

        alert.Severity.Should().Be("danger");
        alert.Title.Should().Contain("Đã vượt hạn mức");
    }

    [Fact]
    public void CategoryBudgetThatDidNotExistBefore_IsIgnored()
    {
        var after = Snapshot(categories: new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 900_000) });

        BudgetAlertPolicy.Detect(Snapshot(), after).Should().BeEmpty();
    }

    [Fact]
    public void OverallAndCategoryBothCrossing_RaisesTwoAlerts_OverallFirstWhenSameLevel()
    {
        var before = Snapshot(new[] { Overall(BudgetPeriod.Week, 400_000) }, new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 400_000) });
        var after = Snapshot(new[] { Overall(BudgetPeriod.Week, 600_000) }, new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 600_000) });

        var alerts = BudgetAlertPolicy.Detect(before, after);

        alerts.Should().HaveCount(2);
        alerts[0].Title.Should().StartWith("Hạn mức tuần:");
        alerts[1].Title.Should().StartWith("Hạn mức tuần · Ăn uống:");
    }

    [Fact]
    public void ManyAlertsAtOnce_AreCappedAtThree_MostSevereFirst()
    {
        var before = Snapshot(
            new[] { Overall(BudgetPeriod.Week, 100_000), Overall(BudgetPeriod.Month, 100_000) },
            new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 100_000), Cat(2, "Mua sắm", BudgetPeriod.Month, 100_000) });
        var after = Snapshot(
            new[] { Overall(BudgetPeriod.Week, 950_000), Overall(BudgetPeriod.Month, 500_000) },       // Sắp chạm, Một nửa
            new[] { Cat(1, "Ăn uống", BudgetPeriod.Week, 800_000), Cat(2, "Mua sắm", BudgetPeriod.Month, 1_200_000) }); // 75%, Vượt

        var alerts = BudgetAlertPolicy.Detect(before, after);

        alerts.Should().HaveCount(BudgetAlertPolicy.MaxAlerts).And.HaveCount(3);
        alerts[0].Title.Should().Contain("Mua sắm").And.Contain("Đã vượt");
        alerts[1].Title.Should().Contain("Sắp chạm");
        alerts[2].Title.Should().Contain("Ăn uống").And.Contain("75%");
    }

    [Fact]
    public void OverallOnlyListOverload_StillReturnsEveryAlert()
    {
        var before = new[] { Overall(BudgetPeriod.Week, 400_000), Overall(BudgetPeriod.Month, 400_000) };
        var after = new[] { Overall(BudgetPeriod.Week, 600_000), Overall(BudgetPeriod.Month, 950_000) };

        BudgetAlertPolicy.Detect(before, after).Should().HaveCount(2);
    }
}
