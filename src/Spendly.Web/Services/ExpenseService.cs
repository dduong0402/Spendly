using Microsoft.EntityFrameworkCore;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Domain;
using Spendly.Web.Models.Expenses;

namespace Spendly.Web.Services;

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public ExpenseService(AppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ExpenseListResult> ListAsync(ExpenseFilter filter, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        var page = Math.Max(1, filter.Page);

        var from = filter.From;
        var to = filter.To;
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var query = _db.Expenses.AsNoTracking().Where(e => e.UserId == userId);

        if (from is { } fromDate)
        {
            query = query.Where(e => e.SpentAt >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(e => e.SpentAt <= toDate);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(e => e.CategoryId == categoryId);
        }

        var keyword = filter.Q?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(e =>
                (e.Note != null && e.Note.Contains(keyword)) || e.Category!.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalAmount = await query.SumAsync(e => (long?)e.Amount, cancellationToken) ?? 0;

        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(e => e.SpentAt)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExpenseListItem(
                e.Id,
                e.SpentAt,
                e.Amount,
                e.Note,
                e.CategoryId,
                e.Category!.Name,
                e.Category!.Color,
                e.Category!.Icon,
                e.RecurringExpenseId != null))
            .ToListAsync(cancellationToken);

        return new ExpenseListResult(items, page, pageSize, totalCount, totalAmount);
    }

    public async Task<ExpenseDetail?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        return await _db.Expenses
            .AsNoTracking()
            .Where(e => e.Id == id && e.UserId == userId)
            .Select(e => new ExpenseDetail(e.Id, e.Amount, e.CategoryId, e.SpentAt, e.Note))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<int>> CreateAsync(SaveExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        request = Normalize(request);

        var error = ExpenseRules.Validate(request, _clock.GetToday());
        if (error is not null)
        {
            return ServiceResult<int>.Invalid(error);
        }

        if (!await IsCategoryAccessibleAsync(userId, request.CategoryId, cancellationToken))
        {
            return ServiceResult<int>.Invalid("Danh mục không hợp lệ.");
        }

        var expense = new Expense
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            SpentAt = request.SpentAt,
            Note = request.Note
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<int>.Ok(expense.Id);
    }

    public async Task<ServiceResult> UpdateAsync(int id, SaveExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (expense is null)
        {
            return ServiceResult.NotFound();
        }

        request = Normalize(request);

        var error = ExpenseRules.Validate(request, _clock.GetToday());
        if (error is not null)
        {
            return ServiceResult.Invalid(error);
        }

        if (!await IsCategoryAccessibleAsync(userId, request.CategoryId, cancellationToken))
        {
            return ServiceResult.Invalid("Danh mục không hợp lệ.");
        }

        expense.Amount = request.Amount;
        expense.CategoryId = request.CategoryId;
        expense.SpentAt = request.SpentAt;
        expense.Note = request.Note;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (expense.RecurringExpenseId is not null)
        {
            // Khoản chi định kỳ đổi sang ngày đã có khoản chi cùng quy tắc (chỉ mục duy nhất).
            return ServiceResult.Conflict("Quy tắc định kỳ này đã có một khoản chi vào ngày đó.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (expense is null)
        {
            return ServiceResult.NotFound();
        }

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Ok();
    }

    private static SaveExpenseRequest Normalize(SaveExpenseRequest request)
    {
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        return request with { Note = note };
    }

    /// <summary>Danh mục dùng được: danh mục mặc định của hệ thống hoặc do chính người dùng tạo.</summary>
    private Task<bool> IsCategoryAccessibleAsync(string userId, int categoryId, CancellationToken cancellationToken) =>
        _db.Categories.AnyAsync(c => c.Id == categoryId && (c.UserId == null || c.UserId == userId), cancellationToken);
}
