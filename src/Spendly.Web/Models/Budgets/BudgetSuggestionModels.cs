using Spendly.Web.Domain;

namespace Spendly.Web.Models.Budgets;

/// <summary>Chi tiêu của một kỳ đã khép lại (một tuần hoặc một tháng) dùng làm căn cứ gợi ý.</summary>
public sealed record SuggestionSample(string Label, long Amount);

/// <summary>
/// Gợi ý hạn mức cho một kỳ: <see cref="Average"/> là trung bình các kỳ đã qua,
/// <see cref="Suggested"/> là mức "sát thực tế" (làm tròn lên), <see cref="Saving"/> là mức tiết kiệm khoảng 10% (null nếu không hợp lý).
/// </summary>
public sealed record BudgetSuggestion(
    BudgetPeriod Period,
    int PeriodsUsed,
    long Average,
    long Suggested,
    long? Saving,
    IReadOnlyList<SuggestionSample> Samples);

public sealed record CategoryBudgetSuggestion(int CategoryId, string CategoryName, BudgetSuggestion Suggestion);

public sealed record BudgetSuggestions(
    IReadOnlyList<BudgetSuggestion> Overall,
    IReadOnlyList<CategoryBudgetSuggestion> Categories)
{
    public static BudgetSuggestions Empty { get; } = new(Array.Empty<BudgetSuggestion>(), Array.Empty<CategoryBudgetSuggestion>());

    public BudgetSuggestion? ForOverall(BudgetPeriod period) => Overall.FirstOrDefault(s => s.Period == period);

    public BudgetSuggestion? ForCategory(int categoryId, BudgetPeriod period) =>
        Categories.FirstOrDefault(c => c.CategoryId == categoryId && c.Suggestion.Period == period)?.Suggestion;
}

/// <summary>Dữ liệu cho partial _BudgetSuggestionHint: nút bấm sẽ điền số tiền vào ô nhập có selector <see cref="TargetSelector"/>.</summary>
public sealed record BudgetSuggestionHint(BudgetPeriod Period, BudgetSuggestion? Suggestion, string TargetSelector);
