using Spendly.Web.Models.Budgets;

namespace Spendly.Web.Services;

public interface IBudgetSuggestionService
{
    /// <summary>Gợi ý hạn mức tuần/tháng (tổng và theo từng danh mục) dựa trên các kỳ đã qua của người dùng hiện tại.</summary>
    Task<BudgetSuggestions> GetSuggestionsAsync(CancellationToken cancellationToken = default);
}
