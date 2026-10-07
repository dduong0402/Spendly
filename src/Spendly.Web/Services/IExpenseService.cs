using Spendly.Web.Common;
using Spendly.Web.Models.Expenses;

namespace Spendly.Web.Services;

public interface IExpenseService
{
    Task<ExpenseListResult> ListAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);

    Task<ExpenseDetail?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> CreateAsync(SaveExpenseRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(int id, SaveExpenseRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
