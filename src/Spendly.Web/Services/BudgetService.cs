using Microsoft.EntityFrameworkCore;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;
using Spendly.Web.Models.Categories;

namespace Spendly.Web.Services;

public class BudgetService : IBudgetService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public BudgetService(AppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BudgetStatus>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var today = _clock.GetToday();

        var budgets = (await _db.Budgets
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .ToListAsync(cancellationToken))
            .OrderBy(b => (int)b.Period)
            .ToList();

        var statuses = new List<BudgetStatus>(budgets.Count);
        foreach (var budget in budgets)
        {
            var range = DateRangeCalculator.GetRange(BudgetEvaluator.ToStatsPeriod(budget.Period), today);
            var from = range.From;
            var to = range.To;

            var inRange = _db.Expenses
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.SpentAt >= from && e.SpentAt <= to);

            var spent = await inRange.SumAsync(e => (long?)e.Amount, cancellationToken) ?? 0;

            // Chỉ cần danh mục chi nhiều nhất khi đã có cảnh báo (từ mốc 50%).
            string? topName = null;
            double? topPercent = null;
            if (BudgetEvaluator.GetLevel(spent, budget.Amount) >= BudgetLevel.Half)
            {
                var top = await inRange
                    .GroupBy(e => new { e.CategoryId, Name = e.Category!.Name })
                    .Select(g => new { g.Key.Name, Amount = g.Sum(e => e.Amount) })
                    .OrderByDescending(x => x.Amount)
                    .FirstOrDefaultAsync(cancellationToken);

                if (top is not null && spent > 0)
                {
                    topName = top.Name;
                    topPercent = top.Amount * 1000 / spent / 10.0;
                }
            }

            statuses.Add(BudgetEvaluator.Evaluate(budget.Period, budget.Amount, spent, today, topName, topPercent));
        }

        return statuses;
    }

    public async Task<ServiceResult> SetAsync(BudgetPeriod period, long amount, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        if (!Enum.IsDefined(period))
        {
            return ServiceResult.Invalid("Loại hạn mức không hợp lệ.");
        }

        if (amount <= 0)
        {
            return ServiceResult.Invalid("Hạn mức phải lớn hơn 0.");
        }

        if (amount > ExpenseRules.MaxAmount)
        {
            return ServiceResult.Invalid("Hạn mức quá lớn.");
        }

        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period, cancellationToken);
        if (budget is null)
        {
            _db.Budgets.Add(new Budget { UserId = userId, Period = period, Amount = amount });
        }
        else
        {
            budget.Amount = amount;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hiếm gặp: hai yêu cầu cùng lúc tạo hạn mức cho một loại kỳ (vi phạm chỉ mục duy nhất).
            return ServiceResult.Conflict("Không thể lưu hạn mức. Vui lòng thử lại.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(BudgetPeriod period, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.UserId == userId && b.Period == period, cancellationToken);
        if (budget is null)
        {
            return ServiceResult.NotFound();
        }

        _db.Budgets.Remove(budget);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Ok();
    }
    public async Task<IReadOnlyList<CategoryBudgetStatus>> GetCategoryStatusesAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var today = _clock.GetToday();

        var rows = await _db.CategoryBudgets
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .Select(b => new
            {
                b.CategoryId,
                b.Period,
                b.Amount,
                Name = b.Category!.Name,
                Color = b.Category!.Color,
                Icon = b.Category!.Icon
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<CategoryBudgetStatus>();
        }

        // Mỗi loại kỳ (tuần/tháng) chỉ cần một truy vấn gom nhóm theo danh mục.
        var spentByPeriod = new Dictionary<BudgetPeriod, Dictionary<int, long>>();
        foreach (var period in rows.Select(r => r.Period).Distinct())
        {
            var range = DateRangeCalculator.GetRange(BudgetEvaluator.ToStatsPeriod(period), today);
            var from = range.From;
            var to = range.To;
            var categoryIds = rows.Where(r => r.Period == period).Select(r => r.CategoryId).ToList();

            var sums = await _db.Expenses
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.SpentAt >= from && e.SpentAt <= to && categoryIds.Contains(e.CategoryId))
                .GroupBy(e => e.CategoryId)
                .Select(g => new { CategoryId = g.Key, Amount = g.Sum(e => e.Amount) })
                .ToListAsync(cancellationToken);

            spentByPeriod[period] = sums.ToDictionary(x => x.CategoryId, x => x.Amount);
        }

        return rows
            .Select(row =>
            {
                spentByPeriod[row.Period].TryGetValue(row.CategoryId, out var spent);
                var status = BudgetEvaluator.Evaluate(row.Period, row.Amount, spent, today, categoryName: row.Name);
                return new CategoryBudgetStatus(status, row.CategoryId, row.Name, row.Color, CategoryIcons.Emoji(row.Icon));
            })
            .OrderByDescending(x => x.Status.Percent)
            .ThenBy(x => (int)x.Status.Period)
            .ThenBy(x => x.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var overall = await GetStatusesAsync(cancellationToken);
        var categories = await GetCategoryStatusesAsync(cancellationToken);
        return new BudgetSnapshot(overall, categories);
    }

    public async Task<ServiceResult> SetCategoryBudgetAsync(int categoryId, BudgetPeriod period, long amount, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        if (!Enum.IsDefined(period))
        {
            return ServiceResult.Invalid("Loại hạn mức không hợp lệ.");
        }

        if (amount <= 0)
        {
            return ServiceResult.Invalid("Hạn mức phải lớn hơn 0.");
        }

        if (amount > ExpenseRules.MaxAmount)
        {
            return ServiceResult.Invalid("Hạn mức quá lớn.");
        }

        // Dùng được: danh mục mặc định của hệ thống hoặc danh mục do chính người dùng tạo.
        var categoryUsable = await _db.Categories.AnyAsync(
            c => c.Id == categoryId && (c.UserId == null || c.UserId == userId),
            cancellationToken);
        if (!categoryUsable)
        {
            return ServiceResult.Invalid("Danh mục không hợp lệ.");
        }

        var budget = await _db.CategoryBudgets.FirstOrDefaultAsync(
            b => b.UserId == userId && b.CategoryId == categoryId && b.Period == period,
            cancellationToken);

        if (budget is null)
        {
            _db.CategoryBudgets.Add(new CategoryBudget { UserId = userId, CategoryId = categoryId, Period = period, Amount = amount });
        }
        else
        {
            budget.Amount = amount;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict("Không thể lưu hạn mức. Vui lòng thử lại.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteCategoryBudgetAsync(int categoryId, BudgetPeriod period, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var budget = await _db.CategoryBudgets.FirstOrDefaultAsync(
            b => b.UserId == userId && b.CategoryId == categoryId && b.Period == period,
            cancellationToken);
        if (budget is null)
        {
            return ServiceResult.NotFound();
        }

        _db.CategoryBudgets.Remove(budget);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Ok();
    }
}
