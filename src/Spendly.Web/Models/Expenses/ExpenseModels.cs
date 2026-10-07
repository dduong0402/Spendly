using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Spendly.Web.Models.Expenses;

public sealed record ExpenseListItem(
    int Id,
    DateOnly SpentAt,
    long Amount,
    string? Note,
    int CategoryId,
    string CategoryName,
    string CategoryColor,
    string CategoryIcon,
    bool IsRecurring);

public sealed record ExpenseDetail(int Id, long Amount, int CategoryId, DateOnly SpentAt, string? Note);

public sealed record SaveExpenseRequest(long Amount, int CategoryId, DateOnly SpentAt, string? Note);

/// <summary>Bộ lọc danh sách chi tiêu, bind từ query string (?From=&amp;To=&amp;CategoryId=&amp;Q=&amp;Page=).</summary>
public class ExpenseFilter
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public int? CategoryId { get; set; }

    public string? Q { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

public sealed record ExpenseListResult(
    IReadOnlyList<ExpenseListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    long TotalAmount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class ExpenseIndexViewModel
{
    public ExpenseFilter Filter { get; set; } = new();

    public ExpenseListResult Result { get; set; } = new(Array.Empty<ExpenseListItem>(), 1, 10, 0, 0);

    public List<SelectListItem> Categories { get; set; } = new();
}

public class ExpenseFormViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập số tiền.")]
    [Display(Name = "Số tiền (₫)")]
    public string AmountInput { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    [Display(Name = "Danh mục")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày chi.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Ngày chi")]
    public DateOnly? SpentAt { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    [BindNever]
    [ValidateNever]
    public List<SelectListItem> Categories { get; set; } = new();
}
