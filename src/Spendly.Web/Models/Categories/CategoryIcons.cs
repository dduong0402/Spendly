using System.Globalization;
using System.Text;

namespace Spendly.Web.Models.Categories;

/// <summary>
/// Biểu tượng danh mục là MỘT emoji bất kỳ do người dùng chọn (lưu thẳng ký tự emoji vào cột Icon).
/// Các bước trước từng lưu tên icon dạng khóa ("utensils"...), lớp này vẫn đọc được các khóa cũ đó.
/// </summary>
public static class CategoryIcons
{
    public const string DefaultEmoji = "📌";

    /// <summary>Khớp độ dài cột Icon trong database.</summary>
    public const int MaxLength = 30;

    private static readonly IReadOnlyDictionary<string, string> Legacy = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["utensils"] = "🍽️",
        ["coffee"] = "☕",
        ["car"] = "🚗",
        ["home"] = "🏠",
        ["receipt"] = "🧾",
        ["shopping-bag"] = "🛍️",
        ["shirt"] = "👕",
        ["film"] = "🎬",
        ["gamepad"] = "🎮",
        ["heart-pulse"] = "🩺",
        ["graduation-cap"] = "🎓",
        ["book"] = "📚",
        ["phone"] = "📱",
        ["plane"] = "✈️",
        ["gift"] = "🎁",
        ["paw"] = "🐾",
        ["ellipsis"] = "📌"
    };

    /// <summary>Các khóa icon cũ cần được đổi sang emoji.</summary>
    public static readonly IReadOnlyList<string> LegacyKeys = Legacy.Keys.ToList();

    /// <summary>Icon dùng để hiển thị: khóa cũ đổi sang emoji, emoji hợp lệ giữ nguyên, còn lại dùng icon mặc định.</summary>
    public static string Resolve(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return DefaultEmoji;
        }

        if (Legacy.TryGetValue(stored, out var emoji))
        {
            return emoji;
        }

        return IsValid(stored) ? stored : DefaultEmoji;
    }

    /// <summary>Giữ tên cũ để các chỗ đang dùng (view, service) không phải sửa.</summary>
    public static string Emoji(string? stored) => Resolve(stored);

    /// <summary>
    /// Hợp lệ khi là đúng MỘT ký tự hiển thị (grapheme, gồm cả emoji ghép như 👍🏽) và có chứa ký hiệu (emoji/biểu tượng).
    /// Chữ cái, chữ số, khoảng trắng, nhiều emoji cùng lúc đều bị từ chối.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (new StringInfo(value).LengthInTextElements != 1)
        {
            return false;
        }

        var hasSymbol = false;
        foreach (var rune in value.EnumerateRunes())
        {
            // Chuỗi UTF-16 hỏng (surrogate lẻ) được .NET đổi thành U+FFFD, không chấp nhận.
            if (rune.Value == 0xFFFD)
            {
                return false;
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (category == UnicodeCategory.Control)
            {
                return false;
            }

            if (category == UnicodeCategory.OtherSymbol)
            {
                hasSymbol = true;
            }
        }

        return hasSymbol;
    }
}
