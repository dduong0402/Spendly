using Microsoft.AspNetCore.Mvc;
using Spendly.Web.Services;

namespace Spendly.Web.ViewComponents;

/// <summary>Khối "Hạn mức" hiển thị trên Dashboard (hạn mức tổng của tuần/tháng hiện tại và hạn mức theo danh mục).</summary>
public class BudgetSummaryViewComponent : ViewComponent
{
    private readonly IBudgetService _budgets;

    public BudgetSummaryViewComponent(IBudgetService budgets)
    {
        _budgets = budgets;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        return View(await _budgets.GetSnapshotAsync());
    }
}
