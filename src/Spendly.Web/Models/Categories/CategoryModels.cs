using System.ComponentModel.DataAnnotations;

namespace Spendly.Web.Models.Categories;

public sealed record CategoryItem(
    int Id,
    string Name,
    string Color,
    string Icon,
    bool IsSystemDefault,
    int ExpenseCount);

public sealed record SaveCategoryRequest(string Name, string Color, string Icon);

/// <summary>Dữ liệu cho partial _CategoryChip (chấm màu + emoji + tên).</summary>
public sealed record CategoryChipViewModel(string Name, string Color, string Icon);

public class CategoryFormViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
    [StringLength(50, ErrorMessage = "Tên danh mục tối đa 50 ký tự.")]
    [Display(Name = "Tên danh mục")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn màu.")]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Màu không hợp lệ.")]
    [Display(Name = "Màu")]
    public string Color { get; set; } = "#1F6BFF";

    [Required(ErrorMessage = "Vui lòng chọn biểu tượng.")]
    [StringLength(CategoryIcons.MaxLength, ErrorMessage = "Biểu tượng không hợp lệ.")]
    [Display(Name = "Biểu tượng")]
    public string Icon { get; set; } = CategoryIcons.DefaultEmoji;
}
