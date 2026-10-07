namespace Spendly.Web.Domain;

/// <summary>Danh mục chi tiêu. UserId = null nghĩa là danh mục mặc định của hệ thống.</summary>
public class Category
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Mã màu hex, ví dụ "#1F6BFF".</summary>
    public string Color { get; set; } = "#1F6BFF";

    /// <summary>Một emoji bất kỳ do người dùng chọn, ví dụ "☕".</summary>
    public string Icon { get; set; } = "📌";

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public bool IsSystemDefault => UserId is null;
}
