using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Spendly.Web.Domain;

namespace Spendly.Web.Models.Recurring;

public sealed record RecurringExpenseItem(
    int Id,
    string Name,
    long Amount,
    int CategoryId,
    string CategoryName,
    string CategoryColor,
    string CategoryIcon,
    RecurrenceFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly NextDueDate,
    bool IsActive,
    bool IsFinished,
    int GeneratedCount,
    long MonthlyEquivalent);

public sealed record RecurringExpenseDetail(
    int Id,
    string Name,
    long Amount,
    int CategoryId,
    RecurrenceFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly NextDueDate,
    bool IsActive,
    bool IsFinished);

public sealed record SaveRecurringRequest(
    string Name,
    long Amount,
    int CategoryId,
    RecurrenceFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate);

/// <summary>Khi sửa chỉ đổi được tên, số tiền, danh mục và ngày kết thúc (chu kỳ và ngày bắt đầu giữ nguyên).</summary>
public sealed record UpdateRecurringRequest(string Name, long Amount, int CategoryId, DateOnly? EndDate);

public sealed record RecurringIndexViewModel(
    IReadOnlyList<RecurringExpenseItem> Items,
    long MonthlyTotal,
    int ActiveCount);

public class RecurringFormViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên khoản chi.")]
    [StringLength(100, ErrorMessage = "Tên tối đa 100 ký tự.")]
    [Display(Name = "Tên khoản chi")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số tiền.")]
    [Display(Name = "Số tiền mỗi lần (₫)")]
    public string AmountInput { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    [Display(Name = "Danh mục")]
    public int? CategoryId { get; set; }

    [Display(Name = "Chu kỳ")]
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Ngày bắt đầu")]
    public DateOnly? StartDate { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Ngày kết thúc (không bắt buộc)")]
    public DateOnly? EndDate { get; set; }

    [BindNever]
    [ValidateNever]
    public List<SelectListItem> Categories { get; set; } = new();
}
