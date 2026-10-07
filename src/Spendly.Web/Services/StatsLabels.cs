using System.Globalization;
using Spendly.Web.Models.Stats;

namespace Spendly.Web.Services;

/// <summary>Nhãn hiển thị tiếng Việt cho thống kê (tách riêng để dễ test).</summary>
public static class StatsLabels
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string RangeLabel(StatsPeriod period, DateRange range) => period switch
    {
        StatsPeriod.Day => range.From.ToString("dd/MM/yyyy", Invariant),
        StatsPeriod.Week =>
            $"Tuần {ISOWeek.GetWeekOfYear(range.From.ToDateTime(TimeOnly.MinValue))}, " +
            $"{range.From.ToString("dd/MM", Invariant)} – {range.To.ToString("dd/MM/yyyy", Invariant)}",
        StatsPeriod.Month => $"Tháng {range.From.Month}/{range.From.Year}",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Kỳ thống kê không hợp lệ.")
    };

    /// <summary>Kỳ đang diễn ra được so với cùng số ngày của kỳ trước nên nhãn nói rõ "cùng kỳ".</summary>
    public static string ComparisonLabel(StatsPeriod period, bool inProgress) => period switch
    {
        StatsPeriod.Day => "so với hôm trước",
        StatsPeriod.Week => inProgress ? "so với cùng kỳ tuần trước" : "so với tuần trước",
        StatsPeriod.Month => inProgress ? "so với cùng kỳ tháng trước" : "so với tháng trước",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Kỳ thống kê không hợp lệ.")
    };

    /// <summary>Nhãn trục X của biểu đồ: tuần = T2..CN, tháng = số ngày, ngày = dd/MM.</summary>
    public static string TrendLabel(StatsPeriod period, DateOnly date) => period switch
    {
        StatsPeriod.Week => WeekdayShort(date.DayOfWeek),
        StatsPeriod.Month => date.Day.ToString(Invariant),
        StatsPeriod.Day => date.ToString("dd/MM", Invariant),
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Kỳ thống kê không hợp lệ.")
    };

    public static string WeekdayShort(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        _ => "CN"
    };

    public static string WeekdayLong(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        _ => "Chủ Nhật"
    };
}
