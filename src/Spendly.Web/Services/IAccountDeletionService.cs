using Spendly.Web.Common;
using Spendly.Web.Domain;

namespace Spendly.Web.Services;

public interface IAccountDeletionService
{
    /// <summary>Xóa VĨNH VIỄN người dùng và mọi dữ liệu của họ trong một giao dịch (lỗi giữa chừng thì không mất gì).</summary>
    Task<ServiceResult> DeleteAccountAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
