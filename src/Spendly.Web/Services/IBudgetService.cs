using Spendly.Web.Common;
using Spendly.Web.Domain;
using Spendly.Web.Models.Budgets;

namespace Spendly.Web.Services;

public interface IBudgetService
{
    /// <summary>Tình hình của kỳ hiện tại với từng hạn mức TỔNG đã đặt (tuần trước, tháng sau). Chưa đặt hạn mức nào thì trả về danh sách rỗng.</summary>
    Task<IReadOnlyList<BudgetStatus>> GetStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>Tình hình của các hạn mức THEO DANH MỤC, mức đã dùng cao nhất lên trước.</summary>
    Task<IReadOnlyList<CategoryBudgetStatus>> GetCategoryStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>Hạn mức tổng + hạn mức theo danh mục (dùng để so sánh trước/sau khi lưu khoản chi).</summary>
    Task<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>Đặt hạn mức tổng: tạo mới hoặc cập nhật nếu đã có hạn mức cho loại kỳ đó.</summary>
    Task<ServiceResult> SetAsync(BudgetPeriod period, long amount, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(BudgetPeriod period, CancellationToken cancellationToken = default);

    /// <summary>Đặt hạn mức cho một danh mục (mặc định hoặc của chính người dùng): tạo mới hoặc cập nhật.</summary>
    Task<ServiceResult> SetCategoryBudgetAsync(int categoryId, BudgetPeriod period, long amount, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteCategoryBudgetAsync(int categoryId, BudgetPeriod period, CancellationToken cancellationToken = default);
}
