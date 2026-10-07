using Spendly.Web.Domain;

namespace Spendly.Web.Services;

/// <summary>Nhãn tiếng Việt cho khoản chi định kỳ (tách riêng để dễ test).</summary>
public static class RecurringLabels
{
    public static string Frequency(RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Weekly => "Hằng tuần",
        RecurrenceFrequency.Monthly => "Hằng tháng",
        RecurrenceFrequency.Yearly => "Hằng năm",
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Chu kỳ không hợp lệ.")
    };

    /// <summary>Mô tả ngày lặp: "Mỗi Thứ Hai", "Ngày 5 hằng tháng", "Ngày 29/02 hằng năm".</summary>
    public static string Schedule(RecurrenceFrequency frequency, DateOnly start) => frequency switch
    {
        RecurrenceFrequency.Weekly => $"Mỗi {StatsLabels.WeekdayLong(start.DayOfWeek)}",
        RecurrenceFrequency.Monthly => start.Day > 28
            ? $"Ngày {start.Day} hằng tháng (tháng ngắn hơn thì ghi vào ngày cuối tháng)"
            : $"Ngày {start.Day} hằng tháng",
        RecurrenceFrequency.Yearly => $"Ngày {start.Day:00}/{start.Month:00} hằng năm",
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Chu kỳ không hợp lệ.")
    };
}
