using Spendly.Web.Domain;

namespace Spendly.Web.Common;

/// <summary>
/// Tính ngày đến hạn của khoản chi định kỳ. Mọi ngày đều tính từ ngày bắt đầu (neo) chứ không cộng dồn từ lần trước,
/// nên bắt đầu 31/01 thì tháng 2 rơi vào 28/02 (hoặc 29/02) nhưng tháng 3 vẫn là 31/03.
/// Đây là nơi DUY NHẤT định nghĩa chu kỳ, mọi nơi khác phải gọi lại đây.
/// </summary>
public static class RecurrenceCalculator
{
    /// <summary>Lần đến hạn thứ <paramref name="index"/> (0 = ngày bắt đầu).</summary>
    public static DateOnly Occurrence(DateOnly start, RecurrenceFrequency frequency, int index) => frequency switch
    {
        RecurrenceFrequency.Weekly => start.AddDays(7 * index),
        RecurrenceFrequency.Monthly => start.AddMonths(index),
        RecurrenceFrequency.Yearly => start.AddYears(index),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Chu kỳ không hợp lệ.")
    };

    /// <summary>Lần đến hạn đầu tiên có ngày &gt;= <paramref name="date"/> (không bao giờ sớm hơn ngày bắt đầu).</summary>
    public static DateOnly FirstOnOrAfter(DateOnly start, RecurrenceFrequency frequency, DateOnly date)
    {
        if (date <= start)
        {
            return start;
        }

        var index = frequency switch
        {
            RecurrenceFrequency.Weekly => (date.DayNumber - start.DayNumber) / 7,
            RecurrenceFrequency.Monthly => (date.Year - start.Year) * 12 + date.Month - start.Month,
            RecurrenceFrequency.Yearly => date.Year - start.Year,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Chu kỳ không hợp lệ.")
        };

        index = Math.Max(0, index);
        while (Occurrence(start, frequency, index) < date)
        {
            index++;
        }

        while (index > 0 && Occurrence(start, frequency, index - 1) >= date)
        {
            index--;
        }

        return Occurrence(start, frequency, index);
    }

    /// <summary>Lần đến hạn ngay sau <paramref name="current"/>.</summary>
    public static DateOnly NextAfter(DateOnly start, RecurrenceFrequency frequency, DateOnly current) =>
        FirstOnOrAfter(start, frequency, current.AddDays(1));

    /// <summary>Quy đổi số tiền mỗi lần ra số tiền ước tính mỗi tháng (làm tròn, dùng số nguyên).</summary>
    public static long MonthlyEquivalent(long amount, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Weekly => (amount * 52 + 6) / 12,
        RecurrenceFrequency.Monthly => amount,
        RecurrenceFrequency.Yearly => (amount + 6) / 12,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Chu kỳ không hợp lệ.")
    };
}
