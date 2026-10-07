using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Spendly.Web.Common;
using Spendly.Web.Domain;
using Spendly.Web.Models.Categories;
using Spendly.Web.Models.Recurring;
using Spendly.Web.Services;

namespace Spendly.Web.Controllers;

[Authorize]
public class RecurringController : Controller
{
    private readonly IRecurringExpenseService _recurring;
    private readonly ICategoryService _categories;
    private readonly TimeProvider _clock;

    public RecurringController(IRecurringExpenseService recurring, ICategoryService categories, TimeProvider clock)
    {
        _recurring = recurring;
        _categories = categories;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _recurring.ListAsync();
        var running = items.Where(i => i.IsActive && !i.IsFinished).ToList();

        return View(new RecurringIndexViewModel(items, running.Sum(i => i.MonthlyEquivalent), running.Count));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new RecurringFormViewModel
        {
            StartDate = _clock.GetToday(),
            Categories = await BuildCategoryOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecurringFormViewModel model)
    {
        if (model.StartDate is null)
        {
            ModelState.AddModelError(nameof(model.StartDate), "Vui lòng chọn ngày bắt đầu.");
        }

        if (TryParseAmount(model, out var amount) && ModelState.IsValid)
        {
            var result = await _recurring.CreateAsync(new SaveRecurringRequest(
                model.Name, amount, model.CategoryId!.Value, model.Frequency, model.StartDate!.Value, model.EndDate));

            if (result.IsSuccess)
            {
                // Bắt đầu ở quá khứ hoặc hôm nay thì ghi ngay các lần đã đến hạn.
                var generated = await _recurring.GenerateDueAsync();
                TempData["Success"] = generated > 0
                    ? $"Đã tạo khoản chi định kỳ và ghi {generated} khoản chi đến hôm nay."
                    : "Đã tạo khoản chi định kỳ. Khoản chi sẽ tự được ghi khi đến hạn.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể lưu khoản chi định kỳ.");
        }

        model.Categories = await BuildCategoryOptionsAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var detail = await _recurring.GetAsync(id);
        if (detail is null)
        {
            return NotFound();
        }

        var model = new RecurringFormViewModel
        {
            Name = detail.Name,
            AmountInput = MoneyFormatter.FormatNumber(detail.Amount),
            CategoryId = detail.CategoryId,
            Frequency = detail.Frequency,
            StartDate = detail.StartDate,
            EndDate = detail.EndDate,
            Categories = await BuildCategoryOptionsAsync()
        };

        ViewData["Detail"] = detail;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RecurringFormViewModel model)
    {
        var detail = await _recurring.GetAsync(id);
        if (detail is null)
        {
            return NotFound();
        }

        if (TryParseAmount(model, out var amount) && ModelState.IsValid)
        {
            var result = await _recurring.UpdateAsync(id, new UpdateRecurringRequest(model.Name, amount, model.CategoryId!.Value, model.EndDate));
            if (result.Status == ServiceStatus.NotFound)
            {
                return NotFound();
            }

            if (result.IsSuccess)
            {
                TempData["Success"] = "Đã cập nhật khoản chi định kỳ. Số tiền mới áp dụng cho các lần sau.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể lưu khoản chi định kỳ.");
        }

        // Chu kỳ và ngày bắt đầu không đổi được: luôn lấy lại từ dữ liệu đã lưu.
        model.Frequency = detail.Frequency;
        model.StartDate = detail.StartDate;
        model.Categories = await BuildCategoryOptionsAsync();
        ViewData["Detail"] = detail;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var result = await _recurring.SetActiveAsync(id, active);
        if (result.Status == ServiceStatus.NotFound)
        {
            return NotFound();
        }

        TempData["Success"] = active ? "Đã tiếp tục khoản chi định kỳ." : "Đã tạm dừng khoản chi định kỳ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _recurring.DeleteAsync(id);
        if (result.Status == ServiceStatus.NotFound)
        {
            return NotFound();
        }

        TempData["Success"] = "Đã xóa khoản chi định kỳ. Các khoản chi đã ghi vẫn được giữ lại.";
        return RedirectToAction(nameof(Index));
    }

    private bool TryParseAmount(RecurringFormViewModel model, out long amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(model.AmountInput))
        {
            return false;
        }

        if (!MoneyParser.TryParse(model.AmountInput, out amount))
        {
            ModelState.AddModelError(nameof(model.AmountInput), "Số tiền không hợp lệ. Ví dụ: 3.500.000");
            return false;
        }

        return true;
    }

    private async Task<List<SelectListItem>> BuildCategoryOptionsAsync()
    {
        var categories = await _categories.ListAsync();
        return categories
            .Select(c => new SelectListItem($"{CategoryIcons.Emoji(c.Icon)} {c.Name}", c.Id.ToString()))
            .ToList();
    }
}
