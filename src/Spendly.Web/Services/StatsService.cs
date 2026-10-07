using Microsoft.EntityFrameworkCore;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Domain;
using Spendly.Web.Models.Categories;
using Spendly.Web.Models.Expenses;
using Spendly.Web.Models.Stats;

namespace Spendly.Web.Services;

public class StatsService : IStatsService
{
    private const int TrendDaysForDayPeriod = 7;
    private const int MaxTake = 20;

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public StatsService(AppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<StatsSummary> GetSummaryAsync(StatsPeriod period, DateOnly date, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var today = _clock.GetToday();

        var range = DateRangeCalculator.GetRange(period, date);
        var previous = DateRangeCalculator.GetPrevious(period, date);

        var hasStarted = range.From <= today;
        var inProgress = hasStarted && today < range.To;

        // Số ngày đã trôi qua trong kỳ (kỳ chưa bắt đầu = 0, kỳ đã kết thúc = đủ ngày).
        var elapsedDays = hasStarted ? Min(range.To, today).DayNumber - range.From.DayNumber + 1 : 0;

        var query = ForRange(userId, range);
        var totalAmount = await query.SumAsync(e => (long?)e.Amount, cancellationToken) ?? 0;
        var transactionCount = await query.CountAsync(cancellationToken);

        // Kỳ đang diễn ra thì chỉ so với cùng số ngày đầu của kỳ trước, để phép so sánh công bằng.
        var previousEnd = inProgress
            ? Min(previous.To, previous.From.AddDays(elapsedDays - 1))
            : previous.To;
        var previousTotal = await ForRange(userId, new DateRange(previous.From, previousEnd))
            .SumAsync(e => (long?)e.Amount, cancellationToken) ?? 0;

        double? changePercent = !hasStarted || previousTotal == 0
            ? null
            : Math.Round((totalAmount - previousTotal) * 100.0 / previousTotal, 1);

        var averagePerDay = elapsedDays == 0
            ? 0
            : (long)Math.Round((decimal)totalAmount / elapsedDays, MidpointRounding.AwayFromZero);

        var previousDate = DateRangeCalculator.Shift(period, date, -1);
        var nextDate = DateRangeCalculator.Shift(period, date, 1);
        var hasNext = DateRangeCalculator.GetRange(period, nextDate).From <= today;

        return new StatsSummary(
            Period: period.ToString().ToLowerInvariant(),
            From: range.From,
            To: range.To,
            RangeLabel: StatsLabels.RangeLabel(period, range),
            ComparisonLabel: StatsLabels.ComparisonLabel(period, inProgress),
            IsCurrent: range.Contains(today),
            TotalAmount: totalAmount,
            TransactionCount: transactionCount,
            AveragePerDay: averagePerDay,
            PreviousTotal: previousTotal,
            ChangePercent: changePercent,
            PreviousDate: previousDate,
            NextDate: nextDate,
            HasNext: hasNext);
    }

    public async Task<IReadOnlyList<TrendPoint>> GetTrendAsync(StatsPeriod period, DateOnly date, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var today = _clock.GetToday();

        var range = period == StatsPeriod.Day
            ? new DateRange(date.AddDays(-(TrendDaysForDayPeriod - 1)), date)
            : DateRangeCalculator.GetRange(period, date);

        var totals = await ForRange(userId, range)
            .GroupBy(e => e.SpentAt)
            .Select(g => new { Date = g.Key, Amount = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        var byDate = totals.ToDictionary(x => x.Date, x => x.Amount);

        var points = new List<TrendPoint>(range.DayCount);
        for (var day = range.From; day <= range.To; day = day.AddDays(1))
        {
            byDate.TryGetValue(day, out var amount);
            points.Add(new TrendPoint(day, StatsLabels.TrendLabel(period, day), amount, day > today));
        }

        return points;
    }

    public async Task<IReadOnlyList<CategoryShare>> GetByCategoryAsync(StatsPeriod period, DateOnly date, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var range = DateRangeCalculator.GetRange(period, date);

        var groups = await ForRange(userId, range)
            .GroupBy(e => new
            {
                e.CategoryId,
                Name = e.Category!.Name,
                Color = e.Category!.Color,
                Icon = e.Category!.Icon
            })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.Name,
                g.Key.Color,
                g.Key.Icon,
                Amount = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var total = groups.Sum(g => g.Amount);
        if (total == 0)
        {
            return Array.Empty<CategoryShare>();
        }

        return groups
            .OrderByDescending(g => g.Amount)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new CategoryShare(
                g.CategoryId,
                g.Name,
                g.Color,
                CategoryIcons.Emoji(g.Icon),
                g.Amount,
                g.Count,
                Math.Round(g.Amount * 100.0 / total, 1)))
            .ToList();
    }

    public async Task<IReadOnlyList<StatsExpenseItem>> GetTopExpensesAsync(StatsPeriod period, DateOnly date, int take = 5, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var range = DateRangeCalculator.GetRange(period, date);

        var items = await Project(ForRange(userId, range)
                .OrderByDescending(e => e.Amount)
                .ThenByDescending(e => e.SpentAt)
                .ThenByDescending(e => e.Id)
                .Take(ClampTake(take)))
            .ToListAsync(cancellationToken);

        return items.Select(ToStatsItem).ToList();
    }

    public async Task<IReadOnlyList<StatsExpenseItem>> GetRecentExpensesAsync(StatsPeriod period, DateOnly date, int take = 5, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var range = DateRangeCalculator.GetRange(period, date);

        var items = await Project(ForRange(userId, range)
                .OrderByDescending(e => e.SpentAt)
                .ThenByDescending(e => e.Id)
                .Take(ClampTake(take)))
            .ToListAsync(cancellationToken);

        return items.Select(ToStatsItem).ToList();
    }

    /// <summary>Chi tiêu của một người dùng trong khoảng ngày (gồm cả hai đầu).</summary>
    private IQueryable<Expense> ForRange(string userId, DateRange range)
    {
        var from = range.From;
        var to = range.To;
        return _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.SpentAt >= from && e.SpentAt <= to);
    }

    private static IQueryable<ExpenseListItem> Project(IQueryable<Expense> query) =>
        query.Select(e => new ExpenseListItem(
            e.Id,
            e.SpentAt,
            e.Amount,
            e.Note,
            e.CategoryId,
            e.Category!.Name,
            e.Category!.Color,
            e.Category!.Icon,
            e.RecurringExpenseId != null));

    private static StatsExpenseItem ToStatsItem(ExpenseListItem item) =>
        new(item.Id, item.SpentAt, item.Amount, item.Note, item.CategoryName, item.CategoryColor, CategoryIcons.Emoji(item.CategoryIcon));

    private static int ClampTake(int take) => Math.Clamp(take, 1, MaxTake);

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
