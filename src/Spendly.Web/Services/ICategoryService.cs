using Spendly.Web.Common;
using Spendly.Web.Models.Categories;

namespace Spendly.Web.Services;

public interface ICategoryService
{
    /// <summary>Danh mục mặc định (theo thứ tự seed) rồi đến danh mục của người dùng (theo tên).</summary>
    Task<IReadOnlyList<CategoryItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Chỉ trả về danh mục do chính người dùng tạo (danh mục mặc định không được sửa/xóa).</summary>
    Task<CategoryItem?> GetOwnAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(int id, SaveCategoryRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
