using System.Globalization;
using System.Text.RegularExpressions;

namespace Spendly.Web.Common;

/// <summary>Đọc số tiền người dùng nhập: "50000", "50.000", "1,250,000", "1 250 000 ₫".</summary>
public static class MoneyParser
{
    // Hoặc chỉ toàn chữ số, hoặc nhóm 3 chữ số ngăn cách bởi . , hoặc khoảng trắng.
    private static readonly Regex Pattern = new(
        @"\A([0-9]+|[0-9]{1,3}([.,\s][0-9]{3})+)\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Separators = new(@"[.,\s]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(string? input, out long amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim().TrimEnd('₫', 'đ', 'Đ').Trim();
        if (!Pattern.IsMatch(text))
        {
            return false;
        }

        var digits = Separators.Replace(text, string.Empty);
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out amount);
    }
}
