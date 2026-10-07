using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Spendly.Web.Common;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Models.Categories;
using Spendly.Web.Models.Expenses;
using Spendly.Web.Services;

namespace Spendly.Web.Controllers;

[Authorize]
public class ExpensesController : Controller
{
    private readonly IExpenseService _expenses;
    private readonly ICategoryService _categories;
    private readonly TimeProvider _clock;
    private readonly IBudgetService _budgets;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(
        IExpenseService expenses,
        ICategoryService categories,
        TimeProvider clock,
        IBudgetService budgets,
        ILogger<ExpensesController> logger)
    {
        _expenses = expenses;
        _categories = categories;
        _clock = clock;
        _budgets = budgets;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ExpenseFilter filter)
    {
        var model = new ExpenseIndexViewModel
        {
            Filter = filter,
            Result = await _expenses.ListAsync(filter),
            Categories = await BuildCategoryOptionsAsync()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ExpenseFormViewModel
        {
            SpentAt = _clock.GetToday(),
            Categories = await BuildCategoryOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExpenseFormViewModel model)
    {
        var request = ToRequest(model);
        if (request is not null)
        {
            var budgetsBefore = await TryGetBudgetStatusesAsync();
            var result = await _expenses.CreateAsync(request);
            if (result.IsSuccess)
            {
                await QueueBudgetAlertsAsync(budgetsBefore);
                TempData["Success"] = "Đã thêm khoản chi.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể lưu khoản chi.");
        }

        model.Categories = await BuildCategoryOptionsAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var detail = await _expenses.GetAsync(id);
        if (detail is null)
        {
            return NotFound();
        }

        var model = new ExpenseFormViewModel
        {
            AmountInput = MoneyFormatter.FormatNumber(detail.Amount),
            CategoryId = detail.CategoryId,
            SpentAt = detail.SpentAt,
            Note = detail.Note,
            Categories = await BuildCategoryOptionsAsync()
        };

        ViewData["ExpenseId"] = id;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ExpenseFormViewModel model)
    {
        var request = ToRequest(model);
        if (request is not null)
        {
            var budgetsBefore = await TryGetBudgetStatusesAsync();
            var result = await _expenses.UpdateAsync(id, request);
            if (result.Status == ServiceStatus.NotFound)
            {
                return NotFound();
            }

            if (result.IsSuccess)
            {
                await QueueBudgetAlertsAsync(budgetsBefore);
                TempData["Success"] = "Đã cập nhật khoản chi.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể lưu khoản chi.");
        }

        model.Categories = await BuildCategoryOptionsAsync();
        ViewData["ExpenseId"] = id;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _expenses.DeleteAsync(id);
        if (result.Status == ServiceStatus.NotFound)
        {
            return NotFound();
        }

        TempData["Success"] = "Đã xóa khoản chi.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Tình hình hạn mức (tổng + theo danh mục) trước khi lưu. Lỗi ở phần hạn mức không được làm hỏng việc lưu chi tiêu.</summary>
    private async Task<BudgetSnapshot?> TryGetBudgetStatusesAsync()
    {
        try
        {
            return await _budgets.GetSnapshotAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đọc được tình hình hạn mức.");
            return null;
        }
    }

    /// <summary>So sánh trước/sau khi lưu; chạm mốc mới hoặc đang vượt hạn mức thì để thông báo hiện ở trang kế tiếp.</summary>
    private async Task QueueBudgetAlertsAsync(BudgetSnapshot? before)
    {
        if (before is null || (before.Overall.Count == 0 && before.Categories.Count == 0))
        {
            return;
        }

        try
        {
            var after = await _budgets.GetSnapshotAsync();
            var alerts = BudgetAlertPolicy.Detect(before, after);
            if (alerts.Count > 0)
            {
                TempData["BudgetAlerts"] = JsonSerializer.Serialize(alerts);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không tính được cảnh báo hạn mức sau khi lưu khoản chi.");
        }
    }

    /// <summary>Chuyển form thành request; trả về null (kèm lỗi trong ModelState) nếu dữ liệu chưa hợp lệ.</summary>
    private SaveExpenseRequest? ToRequest(ExpenseFormViewModel model)
    {
        long amount = 0;
        if (!string.IsNullOrWhiteSpace(model.AmountInput) && !MoneyParser.TryParse(model.AmountInput, out amount))
        {
            ModelState.AddModelError(nameof(model.AmountInput), "Số tiền không hợp lệ. Ví dụ: 50.000");
        }

        if (!ModelState.IsValid)
        {
            return null;
        }

        return new SaveExpenseRequest(amount, model.CategoryId!.Value, model.SpentAt!.Value, model.Note);
    }

    private async Task<List<SelectListItem>> BuildCategoryOptionsAsync()
    {
        var categories = await _categories.ListAsync();
        return categories
            .Select(c => new SelectListItem(
                $"{CategoryIcons.Emoji(c.Icon)} {c.Name}",
                c.Id.ToString()))
            .ToList();
    }
}
