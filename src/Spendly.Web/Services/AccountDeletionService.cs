using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Domain;

namespace Spendly.Web.Services;

public class AccountDeletionService : IAccountDeletionService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountDeletionService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<ServiceResult> DeleteAccountAsync(
    ApplicationUser user,
    CancellationToken cancellationToken = default)
    {
        var userId = user.Id;

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        // 1. Xóa các khoản chi của user.
        await _db.Expenses
            .Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 2. Xóa các khoản chi định kỳ của user.
        await _db.RecurringExpenses
            .Where(r => r.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 3. Xóa các hạn mức của user.
        await _db.CategoryBudgets
            .Where(b => b.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Budgets
            .Where(b => b.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 4. Chỉ xóa Category riêng của user nếu không còn Expense nào
        //    của user khác sử dụng Category đó.
        await _db.Categories
            .Where(c => c.UserId == userId)
            .Where(c => !c.Expenses.Any())
            .ExecuteDeleteAsync(cancellationToken);

        // 5. Vì ExecuteDelete chạy trực tiếp SQL.
        _db.ChangeTracker.Clear();

        // 6. Load lại user sạch sẽ rồi mới xóa Identity user.
        var userToDelete = await _userManager.FindByIdAsync(userId);

        if (userToDelete is null)
        {
            return ServiceResult.Conflict("Không tìm thấy tài khoản.");
        }

        var result = await _userManager.DeleteAsync(userToDelete);

        if (!result.Succeeded)
        {
            return ServiceResult.Conflict(
                result.Errors.FirstOrDefault()?.Description
                ?? "Không thể xóa tài khoản. Vui lòng thử lại.");
        }

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult.Ok();
    }
}
