using System.Text.RegularExpressions;
using Spendly.Web.Models.Categories;

namespace Spendly.Web.Services;

public static class CategoryRules
{
    public const int MaxNameLength = 50;

    private static readonly Regex ColorPattern = new(
        "^#[0-9A-Fa-f]{6}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Trả về thông báo lỗi, hoặc null nếu hợp lệ.</summary>
    public static string? Validate(SaveCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Vui lòng nhập tên danh mục.";
        }

        if (request.Name.Length > MaxNameLength)
        {
            return $"Tên danh mục tối đa {MaxNameLength} ký tự.";
        }

        if (!ColorPattern.IsMatch(request.Color ?? string.Empty))
        {
            return "Màu không hợp lệ.";
        }

        if (!CategoryIcons.IsValid(request.Icon))
        {
            return "Biểu tượng không hợp lệ. Hãy chọn đúng 1 emoji.";
        }

        return null;
    }
}
