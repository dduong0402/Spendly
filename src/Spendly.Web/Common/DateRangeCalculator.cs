using Spendly.Web.Models.Stats;

namespace Spendly.Web.Common;

/// <summary>
/// Tính khoảng ngày cho thống kê. Quy ước: tuần bắt đầu từ Thứ Hai (ISO), tháng là cả tháng dương lịch.
/// Đây là nơi DUY NHẤT định nghĩa khoảng ngày, mọi nơi khác phải gọi lại đây.
/// </summary>
public static class DateRangeCalculator
{
    public static DateRange GetRange(StatsPeriod period, DateOnly date) => period switch
    {
        StatsPeriod.Day => new DateRange(date, date),
        StatsPeriod.Week => GetWeek(date),
        StatsPeriod.Month => GetMonth(date),
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Kỳ thống kê không hợp lệ.")
    };

    /// <summary>Kỳ ngay trước kỳ chứa <paramref name="date"/>.</summary>
    public static DateRange GetPrevious(StatsPeriod period, DateOnly date) =>
        GetRange(period, Shift(period, date, -1));

    /// <summary>Dịch ngày mốc đi <paramref name="steps"/> kỳ (âm = lùi). Tháng ngắn hơn thì tự lấy ngày cuối tháng.</summary>
    public static DateOnly Shift(StatsPeriod period, DateOnly date, int steps) => period switch
    {
        StatsPeriod.Day => date.AddDays(steps),
        StatsPeriod.Week => date.AddDays(7 * steps),
        StatsPeriod.Month => date.AddMonths(steps),
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Kỳ thống kê không hợp lệ.")
    };

    private static DateRange GetWeek(DateOnly date)
    {
        // DayOfWeek: Chủ Nhật = 0, Thứ Hai = 1 ... Thứ Bảy = 6 -> số ngày kể từ Thứ Hai: T2 = 0 ... CN = 6.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        var monday = date.AddDays(-daysSinceMonday);
        return new DateRange(monday, monday.AddDays(6));
    }

    private static DateRange GetMonth(DateOnly date)
    {
        var first = new DateOnly(date.Year, date.Month, 1);
        var last = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
        return new DateRange(first, last);
    }
}
