using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Spendly.Web.Common;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Models.Categories;
using Spendly.Web.Services;

namespace Spendly.Web.Controllers;

[Authorize]
public class BudgetsController : Controller
{
    private readonly IBudgetService _budgets;
    private readonly ICategoryService _categories;
    private readonly IBudgetSuggestionService _suggestions;

    public BudgetsController(IBudgetService budgets, ICategoryService categories, IBudgetSuggestionService suggestions)
    {
        _budgets = budgets;
        _categories = categories;
        _suggestions = suggestions;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var statuses = await _budgets.GetStatusesAsync();
        var categoryBudgets = await _budgets.GetCategoryStatusesAsync();
        var categories = await _categories.ListAsync();
        var suggestions = await _suggestions.GetSuggestionsAsync();

        var cards = new[] { BudgetPeriod.Week, BudgetPeriod.Month }
            .Select(period => new BudgetCardViewModel(
                period,
                Heading: period == BudgetPeriod.Week ? "Hạn mức tuần" : "Hạn mức tháng",
                PeriodWord: period == BudgetPeriod.Week ? "tuần" : "tháng",
                Status: statuses.FirstOrDefault(s => s.Period == period)))
            .ToList();

        var options = categories
            .Select(c => new SelectListItem($"{CategoryIcons.Emoji(c.Icon)} {c.Name}", c.Id.ToString()))
            .ToList();

        return View(new BudgetsIndexViewModel(cards, categoryBudgets, options, suggestions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(BudgetPeriod period, string? amountInput)
    {
        if (!Enum.IsDefined(period))
        {
            return BadRequest();
        }

        if (!MoneyParser.TryParse(amountInput, out var amount))
        {
            TempData["Error"] = "Số tiền không hợp lệ. Ví dụ: 3.000.000";
            return RedirectToAction(nameof(Index));
        }

        var result = await _budgets.SetAsync(period, amount);
        if (result.IsSuccess)
        {
            TempData["Success"] = period == BudgetPeriod.Week ? "Đã lưu hạn mức tuần." : "Đã lưu hạn mức tháng.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không thể lưu hạn mức.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(BudgetPeriod period)
    {
        if (!Enum.IsDefined(period))
        {
            return BadRequest();
        }

        var result = await _budgets.DeleteAsync(period);
        if (result.Status == ServiceStatus.NotFound)
        {
            return NotFound();
        }

        TempData["Success"] = period == BudgetPeriod.Week ? "Đã xóa hạn mức tuần." : "Đã xóa hạn mức tháng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCategory(int categoryId, BudgetPeriod period, string? amountInput)
    {
        if (!Enum.IsDefined(period))
        {
            return BadRequest();
        }

        if (categoryId <= 0)
        {
            TempData["Error"] = "Vui lòng chọn danh mục.";
            return RedirectToAction(nameof(Index));
        }

        if (!MoneyParser.TryParse(amountInput, out var amount))
        {
            TempData["Error"] = "Số tiền không hợp lệ. Ví dụ: 2.000.000";
            return RedirectToAction(nameof(Index));
        }

        var result = await _budgets.SetCategoryBudgetAsync(categoryId, period, amount);
        if (result.IsSuccess)
        {
            TempData["Success"] = "Đã lưu hạn mức theo danh mục.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không thể lưu hạn mức.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int categoryId, BudgetPeriod period)
    {
        if (!Enum.IsDefined(period))
        {
            return BadRequest();
        }

        var result = await _budgets.DeleteCategoryBudgetAsync(categoryId, period);
        if (result.Status == ServiceStatus.NotFound)
        {
            return NotFound();
        }

        TempData["Success"] = "Đã xóa hạn mức theo danh mục.";
        return RedirectToAction(nameof(Index));
    }
}
