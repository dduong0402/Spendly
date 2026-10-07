using Spendly.Web.Models.Stats;

namespace Spendly.Web.Services;

public interface IStatsService
{
    Task<StatsSummary> GetSummaryAsync(StatsPeriod period, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Tuần/Tháng: từng ngày trong kỳ. Ngày: 7 ngày kết thúc tại ngày đó. Ngày không có chi tiêu vẫn có điểm với Amount = 0.</summary>
    Task<IReadOnlyList<TrendPoint>> GetTrendAsync(StatsPeriod period, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryShare>> GetByCategoryAsync(StatsPeriod period, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatsExpenseItem>> GetTopExpensesAsync(StatsPeriod period, DateOnly date, int take = 5, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatsExpenseItem>> GetRecentExpensesAsync(StatsPeriod period, DateOnly date, int take = 5, CancellationToken cancellationToken = default);
}
