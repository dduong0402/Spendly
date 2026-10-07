using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Models.Account;

namespace Spendly.Web.Services;

public class AccountDataService : IAccountDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Giữ nguyên chữ có dấu thay vì \uXXXX để file đọc được.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public AccountDataService(AppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AccountDataSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var expenses = await _db.Expenses.CountAsync(e => e.UserId == userId, cancellationToken);
        var categories = await _db.Categories.CountAsync(c => c.UserId == userId, cancellationToken);
        var recurring = await _db.RecurringExpenses.CountAsync(r => r.UserId == userId, cancellationToken);
        var budgets = await _db.Budgets.CountAsync(b => b.UserId == userId, cancellationToken)
                      + await _db.CategoryBudgets.CountAsync(b => b.UserId == userId, cancellationToken);

        return new AccountDataSummary(expenses, categories, recurring, budgets);
    }

    public async Task<byte[]> ExportCsvAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var rows = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.SpentAt)
            .ThenBy(e => e.Id)
            .Select(e => new { e.SpentAt, e.Amount, CategoryName = e.Category!.Name, e.Note, IsRecurring = e.RecurringExpenseId != null })
            .ToListAsync(cancellationToken);

        return CsvWriter.Build(
            new[] { "Ngày", "Số tiền (VND)", "Danh mục", "Ghi chú", "Định kỳ" },
            rows.Select(r => (IEnumerable<string?>)new[]
            {
                r.SpentAt.ToString("yyyy-MM-dd"),
                r.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.CategoryName,
                r.Note,
                r.IsRecurring ? "Có" : "Không"
            }));
    }

    public async Task<byte[]> ExportJsonAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var account = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.Email, u.CreatedAt })
            .FirstAsync(cancellationToken);

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Name, c.Color, c.Icon })
            .ToListAsync(cancellationToken);

        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.SpentAt)
            .ThenBy(e => e.Id)
            .Select(e => new
            {
                Date = e.SpentAt,
                e.Amount,
                Category = e.Category!.Name,
                e.Note,
                FromRecurringRule = e.RecurringExpense != null ? e.RecurringExpense.Name : null
            })
            .ToListAsync(cancellationToken);

        var budgets = await _db.Budgets
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.Period)
            .Select(b => new { b.Period, b.Amount })
            .ToListAsync(cancellationToken);

        var categoryBudgets = await _db.CategoryBudgets
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.Category!.Name)
            .ThenBy(b => b.Period)
            .Select(b => new { Category = b.Category!.Name, b.Period, b.Amount })
            .ToListAsync(cancellationToken);

        var recurring = await _db.RecurringExpenses
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Name,
                r.Amount,
                Category = r.Category!.Name,
                r.Frequency,
                r.StartDate,
                r.EndDate,
                r.NextDueDate,
                r.IsActive
            })
            .ToListAsync(cancellationToken);

        var export = new
        {
            App = "Spendly",
            ExportedAt = _clock.GetUtcNow(),
            Currency = "VND",
            Account = account,
            Categories = categories,
            Expenses = expenses,
            // Enum xuất dạng chữ (Week/Month, Weekly/Monthly/Yearly) cho dễ đọc; chuyển trong bộ nhớ vì EF không cần dịch sang SQL.
            Budgets = budgets.Select(b => new { Period = b.Period.ToString(), b.Amount }).ToList(),
            CategoryBudgets = categoryBudgets.Select(b => new { b.Category, Period = b.Period.ToString(), b.Amount }).ToList(),
            RecurringExpenses = recurring.Select(r => new
            {
                r.Name, r.Amount, r.Category, Frequency = r.Frequency.ToString(), r.StartDate, r.EndDate, r.NextDueDate, r.IsActive
            }).ToList()
        };

        var options = new JsonSerializerOptions(JsonOptions) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.SerializeToUtf8Bytes(export, options);
    }
}
