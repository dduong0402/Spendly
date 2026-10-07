using System.Globalization;
using Spendly.Web.Common;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Models.Stats;

namespace Spendly.Web.Services;

/// <summary>
/// Quy tắc gợi ý hạn mức từ lịch sử (thuần tính toán, không đụng database):
/// lấy tối đa 3 tuần/tháng đã TRỌN VẸN gần nhất (không tính kỳ hiện tại, bỏ kỳ bắt đầu trước khoản chi đầu tiên của người dùng),
/// gợi ý = trung bình làm tròn LÊN theo bước "đẹp"; mức tiết kiệm ≈ 90% trung bình.
/// </summary>
public static class BudgetSuggestionCalculator
{
    public const int HistoryPeriods = 3;
    private const int SavingPercent = 90;

    /// <summary>Các kỳ đã khép lại dùng làm căn cứ, cũ nhất trước. Rỗng nếu chưa có khoản chi nào hoặc chưa đủ một kỳ trọn vẹn.</summary>
    public static IReadOnlyList<DateRange> GetHistoryRanges(BudgetPeriod period, DateOnly today, DateOnly? firstExpenseDate)
    {
        if (firstExpenseDate is null)
        {
            return Array.Empty<DateRange>();
        }

        var statsPeriod = BudgetEvaluator.ToStatsPeriod(period);
        var ranges = new List<DateRange>();
        for (var back = HistoryPeriods; back >= 1; back--)
        {
            var range = DateRangeCalculator.GetRange(statsPeriod, DateRangeCalculator.Shift(statsPeriod, today, -back));
            if (range.From >= firstExpenseDate.Value)
            {
                ranges.Add(range);
            }
        }

        return ranges;
    }

    public static string SampleLabel(BudgetPeriod period, DateRange range) => period == BudgetPeriod.Week
        ? $"Tuần {ISOWeek.GetWeekOfYear(range.From.ToDateTime(TimeOnly.MinValue))}"
        : $"T{range.From.Month}/{range.From.Year}";

    /// <summary>Null nếu không có kỳ nào hoặc tổng chi bằng 0 (không có gì để gợi ý).</summary>
    public static BudgetSuggestion? Build(BudgetPeriod period, IReadOnlyList<SuggestionSample> samples)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        var total = samples.Sum(s => s.Amount);
        if (total <= 0)
        {
            return null;
        }

        var count = samples.Count;
        var average = (total + count / 2) / count;
        var step = NiceStep(average);

        var suggested = Math.Min(RoundUp(average, step), ExpenseRules.MaxAmount);

        long? saving = null;
        var savingValue = RoundNearest(average * SavingPercent / 100, step);
        if (savingValue > 0 && savingValue < suggested)
        {
            saving = savingValue;
        }

        return new BudgetSuggestion(period, count, average, suggested, saving, samples);
    }

    /// <summary>Bước làm tròn: dưới 100k -> 10k, dưới 1 triệu -> 50k, dưới 10 triệu -> 100k, còn lại 500k.</summary>
    public static long NiceStep(long amount) => amount switch
    {
        < 100_000 => 10_000,
        < 1_000_000 => 50_000,
        < 10_000_000 => 100_000,
        _ => 500_000
    };

    private static long RoundUp(long value, long step) => (value + step - 1) / step * step;

    private static long RoundNearest(long value, long step) => (value + step / 2) / step * step;
}
