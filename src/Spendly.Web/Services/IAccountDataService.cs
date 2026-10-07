using Spendly.Web.Models.Account;

namespace Spendly.Web.Services;

public interface IAccountDataService
{
    Task<AccountDataSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Toàn bộ khoản chi dạng CSV (mở được bằng Excel).</summary>
    Task<byte[]> ExportCsvAsync(CancellationToken cancellationToken = default);

    /// <summary>Toàn bộ dữ liệu của tài khoản dạng JSON: khoản chi, danh mục riêng, hạn mức, khoản định kỳ.</summary>
    Task<byte[]> ExportJsonAsync(CancellationToken cancellationToken = default);
}
