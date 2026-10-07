using Microsoft.EntityFrameworkCore;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Models.Stats;

namespace Spendly.Web.Services;

public class BudgetSuggestionService : IBudgetSuggestionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public BudgetSuggestionService(AppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    private sealed record DailyCategoryTotal(int CategoryId, DateOnly SpentAt, long Amount);

    public async Task<BudgetSuggestions> GetSuggestionsAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var today = _clock.GetToday();

        var firstExpense = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .MinAsync(e => (DateOnly?)e.SpentAt, cancellationToken);

        if (firstExpense is null)
        {
            return BudgetSuggestions.Empty;
        }

        var overall = new List<BudgetSuggestion>();
        var perCategory = new List<(int CategoryId, BudgetSuggestion Suggestion)>();

        foreach (var period in new[] { BudgetPeriod.Week, BudgetPeriod.Month })
        {
            var ranges = BudgetSuggestionCalculator.GetHistoryRanges(period, today, firstExpense);
            if (ranges.Count == 0)
            {
                continue;
            }

            var from = ranges.Min(r => r.From);
            var to = ranges.Max(r => r.To);

            // Một truy vấn gom theo (danh mục, ngày) cho cả cửa sổ lịch sử, phần còn lại tính trong bộ nhớ.
            var rows = await _db.Expenses
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.SpentAt >= from && e.SpentAt <= to)
                .GroupBy(e => new { e.CategoryId, e.SpentAt })
                .Select(g => new DailyCategoryTotal(g.Key.CategoryId, g.Key.SpentAt, g.Sum(e => e.Amount)))
                .ToListAsync(cancellationToken);

            var overallSamples = ranges
                .Select(r => new SuggestionSample(
                    BudgetSuggestionCalculator.SampleLabel(period, r),
                    rows.Where(x => r.Contains(x.SpentAt)).Sum(x => x.Amount)))
                .ToList();

            var overallSuggestion = BudgetSuggestionCalculator.Build(period, overallSamples);
            if (overallSuggestion is not null)
            {
                overall.Add(overallSuggestion);
            }

            foreach (var group in rows.GroupBy(x => x.CategoryId))
            {
                var samples = ranges
                    .Select(r => new SuggestionSample(
                        BudgetSuggestionCalculator.SampleLabel(period, r),
                        group.Where(x => r.Contains(x.SpentAt)).Sum(x => x.Amount)))
                    .ToList();

                var suggestion = BudgetSuggestionCalculator.Build(period, samples);
                if (suggestion is not null)
                {
                    perCategory.Add((group.Key, suggestion));
                }
            }
        }

        if (overall.Count == 0 && perCategory.Count == 0)
        {
            return BudgetSuggestions.Empty;
        }

        var ids = perCategory.Select(x => x.CategoryId).Distinct().ToList();
        var names = await _db.Categories
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var categories = perCategory
            .Where(x => names.ContainsKey(x.CategoryId))
            .OrderByDescending(x => x.Suggestion.Average)
            .Select(x => new CategoryBudgetSuggestion(x.CategoryId, names[x.CategoryId], x.Suggestion))
            .ToList();

        return new BudgetSuggestions(overall, categories);
    }
}
