using System.Globalization;
using Spendly.Web.Common;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Models.Stats;

namespace Spendly.Web.Services;

/// <summary>Tính mức độ sử dụng hạn mức và soạn cảnh báo + lời khuyên. Không truy cập database nên test được trực tiếp.</summary>
public static class BudgetEvaluator
{
    public static StatsPeriod ToStatsPeriod(BudgetPeriod period) => period switch
    {
        BudgetPeriod.Week => StatsPeriod.Week,
        BudgetPeriod.Month => StatsPeriod.Month,
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Loại hạn mức không hợp lệ.")
    };

    /// <summary>Mốc cảnh báo. Dùng số nguyên để so sánh chính xác (không sai số số thực).</summary>
    public static BudgetLevel GetLevel(long spent, long limit)
    {
        if (spent > limit) return BudgetLevel.Exceeded;
        if (spent == limit) return BudgetLevel.Reached;
        if (spent * 100 >= limit * 90) return BudgetLevel.Nearly;
        if (spent * 100 >= limit * 75) return BudgetLevel.ThreeQuarters;
        if (spent * 100 >= limit * 50) return BudgetLevel.Half;
        return BudgetLevel.Safe;
    }

    public static string SeverityOf(BudgetLevel level) => level switch
    {
        BudgetLevel.Safe => "success",
        BudgetLevel.Half => "info",
        BudgetLevel.ThreeQuarters => "warning",
        BudgetLevel.Nearly => "warning",
        _ => "danger"
    };

    public static BudgetStatus Evaluate(
        BudgetPeriod period,
        long limit,
        long spent,
        DateOnly today,
        string? topCategoryName = null,
        double? topCategoryPercent = null,
        string? categoryName = null)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Hạn mức phải lớn hơn 0.");
        }

        var range = DateRangeCalculator.GetRange(ToStatsPeriod(period), today);
        var daysTotal = range.DayCount;
        var daysElapsed = today.DayNumber - range.From.DayNumber + 1;
        var daysRemaining = range.To.DayNumber - today.DayNumber + 1; // gồm cả hôm nay

        var remaining = limit - spent;
        var over = Math.Max(0, spent - limit);

        // Làm tròn XUỐNG 1 chữ số thập phân để 99,96% không hiển thị thành "100%" khi chưa chạm hạn mức.
        var percent = spent * 1000 / limit / 10.0;
        var barPercent = (int)Math.Min(100, spent * 100 / limit);

        var suggestedDaily = remaining > 0 ? remaining / daysRemaining : 0;
        var projected = (long)Math.Round((decimal)spent / daysElapsed * daysTotal, MidpointRounding.AwayFromZero);
        var willExceed = projected > limit;

        var level = GetLevel(spent, limit);
        var word = period == BudgetPeriod.Week ? "tuần" : "tháng";
        var elapsedPercent = daysElapsed * 100.0 / daysTotal;

        // Hạn mức tổng: "hạn mức tuần". Hạn mức danh mục: "hạn mức tuần của Ăn uống".
        var limitText = categoryName is null ? $"hạn mức {word}" : $"hạn mức {word} của {categoryName}";
        var scopeSuffix = categoryName is null ? string.Empty : $" cho {categoryName}";
        var periodLabel = categoryName is null ? $"Hạn mức {word}" : $"Hạn mức {word} · {categoryName}";

        var (title, message, advice) = Compose(
            level, word, limitText, scopeSuffix, limit, spent, remaining, over, percent,
            daysElapsed, daysRemaining, elapsedPercent, suggestedDaily, projected, willExceed,
            topCategoryName, topCategoryPercent);

        return new BudgetStatus(
            Period: period,
            PeriodLabel: periodLabel,
            RangeLabel: StatsLabels.RangeLabel(ToStatsPeriod(period), range),
            From: range.From,
            To: range.To,
            Limit: limit,
            Spent: spent,
            Remaining: remaining,
            OverAmount: over,
            Percent: percent,
            PercentText: FormatPercent(percent),
            BarPercent: barPercent,
            Level: level,
            Severity: SeverityOf(level),
            DaysTotal: daysTotal,
            DaysElapsed: daysElapsed,
            DaysRemaining: daysRemaining,
            SuggestedDailyAllowance: suggestedDaily,
            ProjectedSpend: projected,
            WillExceed: willExceed,
            TopCategoryName: topCategoryName,
            TopCategoryPercent: topCategoryPercent,
            Title: title,
            Message: message,
            Advice: advice);
    }

    private static (string Title, string Message, string Advice) Compose(
        BudgetLevel level,
        string word,
        string limitText,
        string scopeSuffix,
        long limit,
        long spent,
        long remaining,
        long over,
        double percent,
        int daysElapsed,
        int daysRemaining,
        double elapsedPercent,
        long suggestedDaily,
        long projected,
        bool willExceed,
        string? topName,
        double? topPercent)
    {
        var pct = FormatPercent(percent);
        var money = (long amount) => MoneyFormatter.Format(amount);

        // Chi nhanh hơn tiến độ của kỳ (ví dụ mới qua 2 ngày mà đã dùng 60% hạn mức).
        var pace = percent > elapsedPercent + 10 ? $" Bạn đang chi nhanh hơn tiến độ của {word}." : string.Empty;

        // Dự báo chỉ đáng tin khi đã có vài ngày dữ liệu.
        var projection = willExceed && daysElapsed >= 3 && daysRemaining > 0
            ? $" Với nhịp hiện tại, cả {word} bạn sẽ chi khoảng {money(projected)}, vượt hạn mức."
            : string.Empty;

        var topNote = topName is not null && topPercent is not null
            ? $" Danh mục chi nhiều nhất: {topName} ({FormatPercent(topPercent.Value)}% tổng chi)."
            : string.Empty;

        var allowance = suggestedDaily > 0
            ? daysRemaining == 1
                ? $" Hôm nay bạn nên chi tối đa khoảng {money(suggestedDaily)}{scopeSuffix}."
                : $" Để không vượt hạn mức, mỗi ngày còn lại (kể cả hôm nay) nên chi tối đa khoảng {money(suggestedDaily)}{scopeSuffix}."
            : string.Empty;

        var overMessage = $"Bạn đã chi {pct}% {limitText} ({money(spent)} trên {money(limit)}).";

        return level switch
        {
            BudgetLevel.Safe => (
                "Trong mức an toàn",
                $"Bạn đã chi {money(spent)} ({pct}%) {limitText}, còn lại {money(remaining)}.",
                allowance.Trim()),

            BudgetLevel.Half => (
                "Đã dùng hơn một nửa hạn mức",
                overMessage,
                $"Hãy xem lại các khoản chi lớn để cân đối phần còn lại.{pace}{projection}{topNote}{allowance}"),

            BudgetLevel.ThreeQuarters => (
                "Đã dùng hơn 75% hạn mức",
                overMessage,
                $"Hãy ưu tiên các khoản thiết yếu và cân nhắc hoãn những khoản chi chưa cần thiết.{pace}{projection}{topNote}{allowance}"),

            BudgetLevel.Nearly => (
                "Sắp chạm hạn mức (trên 90%)",
                $"Bạn đã chi {pct}% {limitText}, chỉ còn {money(remaining)}.",
                $"Chỉ nên chi cho những khoản thật sự cần thiết. Nếu mục tiêu chưa phù hợp, bạn có thể điều chỉnh hạn mức.{topNote}{allowance}"),

            BudgetLevel.Reached => (
                "Đã chạm hạn mức",
                $"Bạn đã chi đúng bằng {limitText} ({money(limit)}).",
                $"Các khoản chi tiếp theo sẽ vượt hạn mức. Bạn vẫn có thể ghi chi tiêu bình thường, hãy cân nhắc trước khi chi thêm.{topNote}"),

            _ => (
                "Đã vượt hạn mức",
                $"Bạn đã vượt {limitText} {money(over)} (chi {pct}% hạn mức).",
                "Bạn vẫn có thể ghi chi tiêu bình thường. Hãy xem lại các khoản chi lớn và hạn chế chi thêm"
                + (daysRemaining == 1 ? " trong hôm nay" : $" trong {daysRemaining} ngày còn lại")
                + $", hoặc điều chỉnh hạn mức nếu chưa phù hợp.{topNote}")
        };
    }

    /// <summary>62.5 -> "62,5"; 50 -> "50".</summary>
    public static string FormatPercent(double percent) =>
        percent.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
}
