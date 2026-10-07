using Microsoft.EntityFrameworkCore;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Domain;
using Spendly.Web.Models.Categories;

namespace Spendly.Web.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CategoryService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CategoryItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var items = await _db.Categories
            .AsNoTracking()
            .Where(c => c.UserId == null || c.UserId == userId)
            .Select(c => new CategoryItem(
                c.Id,
                c.Name,
                c.Color,
                c.Icon,
                c.UserId == null,
                c.Expenses.Count(e => e.UserId == userId)))
            .ToListAsync(cancellationToken);

        return items
            .OrderBy(i => i.IsSystemDefault ? 0 : 1)
            .ThenBy(i => i.IsSystemDefault ? i.Id : 0)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<CategoryItem?> GetOwnAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id && c.UserId == userId)
            .Select(c => new CategoryItem(c.Id, c.Name, c.Color, c.Icon, false, c.Expenses.Count))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<int>> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        request = Normalize(request);

        var error = CategoryRules.Validate(request);
        if (error is not null)
        {
            return ServiceResult<int>.Invalid(error);
        }

        if (await NameExistsAsync(userId, request.Name, excludeId: null, cancellationToken))
        {
            return ServiceResult<int>.Invalid("Tên danh mục đã tồn tại.");
        }

        var category = new Category
        {
            UserId = userId,
            Name = request.Name,
            Color = request.Color,
            Icon = request.Icon
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<int>.Ok(category.Id);
    }

    public async Task<ServiceResult> UpdateAsync(int id, SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (category is null)
        {
            return ServiceResult.NotFound();
        }

        request = Normalize(request);

        var error = CategoryRules.Validate(request);
        if (error is not null)
        {
            return ServiceResult.Invalid(error);
        }

        if (await NameExistsAsync(userId, request.Name, excludeId: id, cancellationToken))
        {
            return ServiceResult.Invalid("Tên danh mục đã tồn tại.");
        }

        category.Name = request.Name;
        category.Color = request.Color;
        category.Icon = request.Icon;
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (category is null)
        {
            return ServiceResult.NotFound();
        }

        var expenseCount = await _db.Expenses.CountAsync(e => e.CategoryId == id, cancellationToken);
        if (expenseCount > 0)
        {
            return ServiceResult.Conflict(
                $"Danh mục đang có {expenseCount} khoản chi. Hãy chuyển các khoản chi sang danh mục khác trước khi xóa.");
        }

        var recurringCount = await _db.RecurringExpenses.CountAsync(r => r.CategoryId == id, cancellationToken);
        if (recurringCount > 0)
        {
            return ServiceResult.Conflict(
                $"Danh mục đang được dùng bởi {recurringCount} khoản chi định kỳ. Hãy đổi danh mục hoặc xóa các khoản định kỳ đó trước.");
        }

        // Hạn mức theo danh mục không còn ý nghĩa khi danh mục bị xóa.
        var categoryBudgets = await _db.CategoryBudgets
            .Where(b => b.CategoryId == id && b.UserId == userId)
            .ToListAsync(cancellationToken);
        _db.CategoryBudgets.RemoveRange(categoryBudgets);

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Ok();
    }

    private static SaveCategoryRequest Normalize(SaveCategoryRequest request) =>
        new((request.Name ?? string.Empty).Trim(), (request.Color ?? string.Empty).Trim().ToUpperInvariant(), (request.Icon ?? string.Empty).Trim());

    /// <summary>Tên trùng với danh mục mặc định hoặc danh mục khác của chính người dùng.</summary>
    private Task<bool> NameExistsAsync(string userId, string name, int? excludeId, CancellationToken cancellationToken) =>
        _db.Categories.AnyAsync(
            c => (c.UserId == null || c.UserId == userId) && c.Name == name && (excludeId == null || c.Id != excludeId),
            cancellationToken);
}
