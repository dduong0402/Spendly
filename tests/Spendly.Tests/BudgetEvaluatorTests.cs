using FluentAssertions;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

/// <summary>Hôm nay trong test: Thứ Ba 29/09/2026. Tuần: 28/09 - 04/10 (đã qua 2/7 ngày). Tháng 9 có 30 ngày (đã qua 29, còn 2 kể cả hôm nay).</summary>
public class BudgetEvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private const long Limit = 1_000_000;

    // ---------- Mốc cảnh báo ----------

    [Theory]
    [InlineData(0L, BudgetLevel.Safe)]
    [InlineData(499_999L, BudgetLevel.Safe)]
    [InlineData(500_000L, BudgetLevel.Half)]
    [InlineData(749_999L, BudgetLevel.Half)]
    [InlineData(750_000L, BudgetLevel.ThreeQuarters)]
    [InlineData(899_999L, BudgetLevel.ThreeQuarters)]
    [InlineData(900_000L, BudgetLevel.Nearly)]
    [InlineData(999_999L, BudgetLevel.Nearly)]
    [InlineData(1_000_000L, BudgetLevel.Reached)]
    [InlineData(1_000_001L, BudgetLevel.Exceeded)]
    [InlineData(5_000_000L, BudgetLevel.Exceeded)]
    public void GetLevel_HonoursExactThresholds(long spent, BudgetLevel expected)
    {
        BudgetEvaluator.GetLevel(spent, Limit).Should().Be(expected);
    }

    [Theory]
    [InlineData(49L, BudgetLevel.Safe)]
    [InlineData(50L, BudgetLevel.Half)]
    [InlineData(75L, BudgetLevel.ThreeQuarters)]
    [InlineData(90L, BudgetLevel.Nearly)]
    [InlineData(100L, BudgetLevel.Reached)]
    [InlineData(101L, BudgetLevel.Exceeded)]
    public void GetLevel_WorksForSmallLimits(long spent, BudgetLevel expected)
    {
        BudgetEvaluator.GetLevel(spent, 100).Should().Be(expected);
    }

    [Theory]
    [InlineData(BudgetLevel.Safe, "success")]
    [InlineData(BudgetLevel.Half, "info")]
    [InlineData(BudgetLevel.ThreeQuarters, "warning")]
    [InlineData(BudgetLevel.Nearly, "warning")]
    [InlineData(BudgetLevel.Reached, "danger")]
    [InlineData(BudgetLevel.Exceeded, "danger")]
    public void SeverityOf_MapsEveryLevel(BudgetLevel level, string expected)
    {
        BudgetEvaluator.SeverityOf(level).Should().Be(expected);
    }

    // ---------- Số liệu ----------

    [Fact]
    public void Evaluate_ComputesPercentAndBar()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 625_000, Today);

        status.Percent.Should().Be(62.5);
        status.PercentText.Should().Be("62,5");
        status.BarPercent.Should().Be(62);
        status.Remaining.Should().Be(375_000);
        status.OverAmount.Should().Be(0);
        status.Level.Should().Be(BudgetLevel.Half);
    }

    [Fact]
    public void Evaluate_WholePercent_HasNoDecimalPart()
    {
        BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 500_000, Today).PercentText.Should().Be("50");
    }

    [Fact]
    public void Evaluate_JustBelowLimit_IsNotRoundedUpTo100()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 999_900, Today);

        status.PercentText.Should().Be("99,9");
        status.Level.Should().Be(BudgetLevel.Nearly);
        status.BarPercent.Should().Be(99);
    }

    [Fact]
    public void Evaluate_Exceeded_CapsBarAndReportsOverAmount()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 1_500_000, Today);

        status.Percent.Should().Be(150);
        status.BarPercent.Should().Be(100);
        status.OverAmount.Should().Be(500_000);
        status.Remaining.Should().Be(-500_000);
        status.SuggestedDailyAllowance.Should().Be(0);
        status.Level.Should().Be(BudgetLevel.Exceeded);
        status.Severity.Should().Be("danger");
    }

    [Fact]
    public void Evaluate_Week_UsesCurrentWeekDays()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 0, Today);

        status.From.Should().Be(new DateOnly(2026, 9, 28));
        status.To.Should().Be(new DateOnly(2026, 10, 4));
        status.DaysTotal.Should().Be(7);
        status.DaysElapsed.Should().Be(2);
        status.DaysRemaining.Should().Be(6);
        status.PeriodLabel.Should().Be("Hạn mức tuần");
        status.RangeLabel.Should().Be("Tuần 40, 28/09 – 04/10/2026");
    }

    [Fact]
    public void Evaluate_Month_UsesCurrentMonthDays()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Month, Limit, 0, Today);

        status.From.Should().Be(new DateOnly(2026, 9, 1));
        status.To.Should().Be(new DateOnly(2026, 9, 30));
        status.DaysTotal.Should().Be(30);
        status.DaysElapsed.Should().Be(29);
        status.DaysRemaining.Should().Be(2);
        status.PeriodLabel.Should().Be("Hạn mức tháng");
        status.RangeLabel.Should().Be("Tháng 9/2026");
    }

    [Fact]
    public void Evaluate_SuggestedDailyAllowance_DividesRemainingByRemainingDays()
    {
        // Còn 600.000 cho 6 ngày (Thứ Ba đến Chủ Nhật).
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, 700_000, 100_000, Today);

        status.SuggestedDailyAllowance.Should().Be(100_000);
    }

    [Fact]
    public void Evaluate_ProjectsSpendFromCurrentPace()
    {
        // 200.000 trong 2 ngày -> 700.000 cho cả tuần.
        var onTrack = BudgetEvaluator.Evaluate(BudgetPeriod.Week, 700_000, 200_000, Today);
        var tooFast = BudgetEvaluator.Evaluate(BudgetPeriod.Week, 600_000, 200_000, Today);

        onTrack.ProjectedSpend.Should().Be(700_000);
        onTrack.WillExceed.Should().BeFalse();
        tooFast.ProjectedSpend.Should().Be(700_000);
        tooFast.WillExceed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NonPositiveLimit_Throws()
    {
        var act = () => BudgetEvaluator.Evaluate(BudgetPeriod.Week, 0, 0, Today);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------- Cảnh báo + lời khuyên ----------

    [Fact]
    public void Safe_HasNoWarningButShowsRemainingAndDailyAllowance()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 100_000, Today);

        status.Level.Should().Be(BudgetLevel.Safe);
        status.Severity.Should().Be("success");
        status.Title.Should().Be("Trong mức an toàn");
        status.Message.Should().Contain("100.000 ₫").And.Contain("còn lại 900.000 ₫");
        status.Advice.Should().Contain("mỗi ngày còn lại").And.Contain("150.000 ₫");
    }

    [Fact]
    public void Half_WarnsAndMentionsTopCategoryPaceAndAllowance()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 500_000, Today, "Ăn uống", 60);

        status.Severity.Should().Be("info");
        status.Title.Should().Contain("một nửa");
        status.Message.Should().Contain("50%");
        status.Advice.Should().Contain("chi nhanh hơn tiến độ"); // mới qua 2/7 ngày mà đã dùng 50%
        status.Advice.Should().Contain("Ăn uống (60% tổng chi)");
        status.Advice.Should().Contain("83.333 ₫");
    }

    [Fact]
    public void Half_LaterInMonth_ProjectsOverspending()
    {
        var midMonth = new DateOnly(2026, 9, 15);

        // 600.000 trong 15 ngày -> khoảng 1.200.000 cho cả tháng, vượt hạn mức 1.000.000.
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Month, Limit, 600_000, midMonth);

        status.WillExceed.Should().BeTrue();
        status.Advice.Should().Contain("Với nhịp hiện tại").And.Contain("1.200.000 ₫");
    }

    [Fact]
    public void ThreeQuarters_AdvisesPrioritisingEssentials()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 800_000, Today);

        status.Severity.Should().Be("warning");
        status.Title.Should().Contain("75%");
        status.Advice.Should().Contain("thiết yếu");
    }

    [Fact]
    public void Nearly_SaysOnlyAmountLeft()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 950_000, Today);

        status.Severity.Should().Be("warning");
        status.Title.Should().Contain("90%");
        status.Message.Should().Contain("chỉ còn 50.000 ₫");
        status.Advice.Should().Contain("thật sự cần thiết");
    }

    [Fact]
    public void Reached_SaysNextExpensesWillExceed_ButStillAllowed()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 1_000_000, Today);

        status.Severity.Should().Be("danger");
        status.Title.Should().Be("Đã chạm hạn mức");
        status.Advice.Should().Contain("vượt hạn mức").And.Contain("vẫn có thể ghi chi tiêu bình thường");
    }

    [Fact]
    public void Exceeded_ReportsOverAmount_AndKeepsUsageNormal()
    {
        var status = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 1_500_000, Today, "Mua sắm", 70);

        status.Title.Should().Be("Đã vượt hạn mức");
        status.Message.Should().Contain("vượt hạn mức tuần").And.Contain("500.000 ₫").And.Contain("150%");
        status.Advice.Should().Contain("vẫn có thể ghi chi tiêu bình thường");
        status.Advice.Should().Contain("trong 6 ngày còn lại");
        status.Advice.Should().Contain("Mua sắm (70% tổng chi)");
    }

    [Fact]
    public void LastDayOfPeriod_TalksAboutToday()
    {
        var sunday = new DateOnly(2026, 10, 4);

        var safe = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 100_000, sunday);
        var exceeded = BudgetEvaluator.Evaluate(BudgetPeriod.Week, Limit, 1_200_000, sunday);

        safe.DaysRemaining.Should().Be(1);
        safe.Advice.Should().Contain("Hôm nay bạn nên chi tối đa khoảng 900.000 ₫");
        exceeded.Advice.Should().Contain("trong hôm nay");
    }
}
