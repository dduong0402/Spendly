using System.Globalization;

namespace Spendly.Web.Common;

/// <summary>Định dạng tiền VND: 1250000 -> "1.250.000 ₫".</summary>
public static class MoneyFormatter
{
    private static readonly NumberFormatInfo Vietnamese = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = new[] { 3 }
    };

    /// <summary>1250000 -> "1.250.000" (không kèm ký hiệu tiền tệ).</summary>
    public static string FormatNumber(long amount) => amount.ToString("N0", Vietnamese);

    public static string Format(long amount) => $"{FormatNumber(amount)} ₫";

    /// <summary>Dạng gọn để ghi trong danh sách: 2900000 -> "2,9 tr", 25000 -> "25k", 1500000000 -> "1,5 tỷ".</summary>
    public static string FormatCompact(long amount)
    {
        if (amount < 0)
        {
            return "-" + FormatCompact(-amount);
        }

        if (amount >= 999_950_000)  // sát 1 tỷ thì làm tròn thành "1 tỷ", không viết "1000 tr"
        {
            return Scaled(amount, 1_000_000_000, " tỷ");
        }

        if (amount >= 999_950)
        {
            return Scaled(amount, 1_000_000, " tr");
        }

        if (amount >= 1_000)
        {
            return Scaled(amount, 1_000, "k");
        }

        return amount.ToString(CultureInfo.InvariantCulture);
    }

    // Làm tròn 1 chữ số thập phân bằng số nguyên (tránh sai số số thực); bỏ ",0" thừa.
    private static string Scaled(long amount, long unit, string suffix)
    {
        var tenths = (amount * 10 + unit / 2) / unit;
        var whole = tenths / 10;
        var fraction = tenths % 10;
        return fraction == 0 ? $"{whole}{suffix}" : $"{whole},{fraction}{suffix}";
    }
}
