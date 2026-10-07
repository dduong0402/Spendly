using Spendly.Web.Common;
using Spendly.Web.Models.Recurring;

namespace Spendly.Web.Services;

public interface IRecurringExpenseService
{
    /// <summary>Các quy tắc của người dùng: đang chạy (sắp đến hạn trước) rồi đến tạm dừng/đã kết thúc.</summary>
    Task<IReadOnlyList<RecurringExpenseItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<RecurringExpenseDetail?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> CreateAsync(SaveRecurringRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(int id, UpdateRecurringRequest request, CancellationToken cancellationToken = default);

    /// <summary>Tạm dừng hoặc tiếp tục. Tiếp tục thì bỏ qua các lần đã lỡ trong lúc dừng, chạy lại từ lần đến hạn kế tiếp tính từ hôm nay.</summary>
    Task<ServiceResult> SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default);

    /// <summary>Xóa quy tắc; các khoản chi đã được ghi vẫn được giữ lại (chỉ gỡ liên kết).</summary>
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Ghi các khoản chi đã đến hạn (kể cả những lần bị lỡ, ghi đúng ngày đến hạn). Trả về số khoản chi vừa tạo.</summary>
    Task<int> GenerateDueAsync(CancellationToken cancellationToken = default);
}
