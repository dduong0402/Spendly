using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spendly.Web.Common;
using Spendly.Web.Models.Categories;
using Spendly.Web.Services;

namespace Spendly.Web.Controllers;

[Authorize]
public class CategoriesController : Controller
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _categories.ListAsync());

    [HttpGet]
    public IActionResult Create() => View(new CategoryFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _categories.CreateAsync(new SaveCategoryRequest(model.Name, model.Color, model.Icon));
            if (result.IsSuccess)
            {
                TempData["Success"] = "Đã thêm danh mục.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể lưu danh mục.");
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _categories.GetOwnAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        ViewData["CategoryId"] = id;
        return View(new CategoryFormViewModel { Name = category.Name, Color = category.Color, Icon = CategoryIcons.Resolve(category.Icon) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _categories.UpdateAsync(id, new SaveCategoryRequest(model.Name, model.Color, model.Icon));
            if (result.Status == ServiceStatus.NotFound)
            {
                return NotFound();
            }

            if (result.IsSuccess)
            {
                TempData["Success"] = "Đã cập nhật danh mục.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể lưu danh mục.");
        }

        ViewData["CategoryId"] = id;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _categories.DeleteAsync(id);
        switch (result.Status)
        {
            case ServiceStatus.NotFound:
                return NotFound();
            case ServiceStatus.Ok:
                TempData["Success"] = "Đã xóa danh mục.";
                break;
            default:
                TempData["Error"] = result.Error ?? "Không thể xóa danh mục.";
                break;
        }

        return RedirectToAction(nameof(Index));
    }
}
