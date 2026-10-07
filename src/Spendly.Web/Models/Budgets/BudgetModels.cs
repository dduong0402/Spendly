using Microsoft.AspNetCore.Mvc.Rendering;
using Spendly.Web.Domain;

namespace Spendly.Web.Models.Budgets;

/// <summary>Các mốc cảnh báo: dưới 50% an toàn, rồi 50% - 75% - 90% - 100% (chạm hạn mức), cuối cùng là vượt hạn mức.</summary>
public enum BudgetLevel
{
    Safe = 0,
    Half = 1,
    ThreeQuarters = 2,
    Nearly = 3,
    Reached = 4,
    Exceeded = 5
}

/// <summary>Tình hình chi tiêu của kỳ HIỆN TẠI so với hạn mức, kèm cảnh báo và lời khuyên.</summary>
public sealed record BudgetStatus(
    BudgetPeriod Period,
    string PeriodLabel,
    string RangeLabel,
    DateOnly From,
    DateOnly To,
    long Limit,
    long Spent,
    long Remaining,
    long OverAmount,
    double Percent,
    string PercentText,
    int BarPercent,
    BudgetLevel Level,
    string Severity,
    int DaysTotal,
    int DaysElapsed,
    int DaysRemaining,
    long SuggestedDailyAllowance,
    long ProjectedSpend,
    bool WillExceed,
    string? TopCategoryName,
    double? TopCategoryPercent,
    string Title,
    string Message,
    string Advice);

/// <summary>Thông báo hiển thị một lần (qua TempData) sau khi người dùng lưu khoản chi làm chạm mốc mới.</summary>
public sealed record BudgetAlertMessage(string Period, string Severity, string Title, string Message, string Advice);

public sealed record BudgetCardViewModel(BudgetPeriod Period, string Heading, string PeriodWord, BudgetStatus? Status);

/// <summary>Tình hình của một hạn mức theo danh mục (Status dùng chung cách tính mốc/cảnh báo với hạn mức tổng).</summary>
public sealed record CategoryBudgetStatus(
    BudgetStatus Status,
    int CategoryId,
    string CategoryName,
    string CategoryColor,
    string CategoryEmoji);

/// <summary>Toàn bộ hạn mức của người dùng: tổng (tuần/tháng) và theo từng danh mục.</summary>
public sealed record BudgetSnapshot(
    IReadOnlyList<BudgetStatus> Overall,
    IReadOnlyList<CategoryBudgetStatus> Categories);

public sealed record BudgetsIndexViewModel(
    IReadOnlyList<BudgetCardViewModel> Cards,
    IReadOnlyList<CategoryBudgetStatus> CategoryBudgets,
    IReadOnlyList<SelectListItem> CategoryOptions,
    BudgetSuggestions Suggestions);
