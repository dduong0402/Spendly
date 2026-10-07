using Microsoft.EntityFrameworkCore;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Domain;
using Spendly.Web.Models.Recurring;

namespace Spendly.Web.Services;

public class RecurringExpenseService : IRecurringExpenseService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public RecurringExpenseService(AppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RecurringExpenseItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var rows = await _db.RecurringExpenses
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Amount,
                r.CategoryId,
                CategoryName = r.Category!.Name,
                CategoryColor = r.Category!.Color,
                CategoryIcon = r.Category!.Icon,
                r.Frequency,
                r.StartDate,
                r.EndDate,
                r.NextDueDate,
                r.IsActive,
                GeneratedCount = r.Expenses.Count
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r =>
            {
                var finished = r.EndDate is { } end && r.NextDueDate > end;
                return new RecurringExpenseItem(
                    r.Id, r.Name, r.Amount, r.CategoryId, r.CategoryName, r.CategoryColor, r.CategoryIcon,
                    r.Frequency, r.StartDate, r.EndDate, r.NextDueDate, r.IsActive, finished, r.GeneratedCount,
                    RecurrenceCalculator.MonthlyEquivalent(r.Amount, r.Frequency));
            })
            .OrderBy(i => i.IsActive && !i.IsFinished ? 0 : 1)
            .ThenBy(i => i.NextDueDate)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<RecurringExpenseDetail?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var r = await _db.RecurringExpenses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        return r is null
            ? null
            : new RecurringExpenseDetail(r.Id, r.Name, r.Amount, r.CategoryId, r.Frequency, r.StartDate, r.EndDate, r.NextDueDate, r.IsActive, r.IsFinished);
    }

    public async Task<ServiceResult<int>> CreateAsync(SaveRecurringRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        request = request with { Name = (request.Name ?? string.Empty).Trim() };

        var error = RecurringExpenseRules.Validate(request, _clock.GetToday());
        if (error is not null)
        {
            return ServiceResult<int>.Invalid(error);
        }

        if (!await IsCategoryAccessibleAsync(userId, request.CategoryId, cancellationToken))
        {
            return ServiceResult<int>.Invalid("Danh mục không hợp lệ.");
        }

        var rule = new RecurringExpense
        {
            UserId = userId,
            Name = request.Name,
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            Frequency = request.Frequency,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NextDueDate = request.StartDate,
            IsActive = true
        };

        _db.RecurringExpenses.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<int>.Ok(rule.Id);
    }

    public async Task<ServiceResult> UpdateAsync(int id, UpdateRecurringRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var rule = await _db.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);
        if (rule is null)
        {
            return ServiceResult.NotFound();
        }

        request = request with { Name = (request.Name ?? string.Empty).Trim() };

        var error = RecurringExpenseRules.Validate(request, rule.StartDate);
        if (error is not null)
        {
            return ServiceResult.Invalid(error);
        }

        if (!await IsCategoryAccessibleAsync(userId, request.CategoryId, cancellationToken))
        {
            return ServiceResult.Invalid("Danh mục không hợp lệ.");
        }

        var wasFinished = rule.IsFinished;

        rule.Name = request.Name;
        rule.Amount = request.Amount;
        rule.CategoryId = request.CategoryId;
        rule.EndDate = request.EndDate;

        // Kéo dài một quy tắc đã kết thúc từ lâu thì chạy tiếp từ lần đến hạn kế tiếp, không ghi bù cả khoảng đã dừng.
        if (wasFinished && !rule.IsFinished)
        {
            var today = _clock.GetToday();
            if (rule.NextDueDate < today)
            {
                rule.NextDueDate = RecurrenceCalculator.FirstOnOrAfter(rule.StartDate, rule.Frequency, today);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var rule = await _db.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);
        if (rule is null)
        {
            return ServiceResult.NotFound();
        }

        if (rule.IsActive == active)
        {
            return ServiceResult.Ok();
        }

        rule.IsActive = active;
        if (active)
        {
            var today = _clock.GetToday();
            if (rule.NextDueDate < today)
            {
                rule.NextDueDate = RecurrenceCalculator.FirstOnOrAfter(rule.StartDate, rule.Frequency, today);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var rule = await _db.RecurringExpenses.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);
        if (rule is null)
        {
            return ServiceResult.NotFound();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Giữ lại các khoản chi đã ghi: chỉ gỡ liên kết tới quy tắc sắp xóa.
        await _db.Expenses
            .Where(e => e.RecurringExpenseId == id && e.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RecurringExpenseId, (int?)null), cancellationToken);

        _db.RecurringExpenses.Remove(rule);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult.Ok();
    }

    public async Task<int> GenerateDueAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var today = _clock.GetToday();

        var rules = await _db.RecurringExpenses
            .Where(r => r.UserId == userId
                        && r.IsActive
                        && r.NextDueDate <= today
                        && (r.EndDate == null || r.NextDueDate <= r.EndDate))
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return 0;
        }

        var created = 0;
        foreach (var rule in rules)
        {
            while (created < RecurringExpenseRules.MaxGeneratedPerRun
                   && rule.NextDueDate <= today
                   && (rule.EndDate is null || rule.NextDueDate <= rule.EndDate))
            {
                _db.Expenses.Add(new Expense
                {
                    UserId = userId,
                    CategoryId = rule.CategoryId,
                    Amount = rule.Amount,
                    SpentAt = rule.NextDueDate,
                    Note = rule.Name,
                    RecurringExpenseId = rule.Id
                });

                created++;
                rule.NextDueDate = RecurrenceCalculator.NextAfter(rule.StartDate, rule.Frequency, rule.NextDueDate);
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai yêu cầu cùng ghi một lần đến hạn (vi phạm chỉ mục duy nhất): yêu cầu kia đã ghi rồi, bỏ phần của mình.
            _db.ChangeTracker.Clear();
            return 0;
        }

        return created;
    }

    private Task<bool> IsCategoryAccessibleAsync(string userId, int categoryId, CancellationToken cancellationToken) =>
        _db.Categories.AnyAsync(c => c.Id == categoryId && (c.UserId == null || c.UserId == userId), cancellationToken);
}
