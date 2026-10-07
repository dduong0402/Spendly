namespace Spendly.Web.Domain;

/// <summary>Hạn mức chi tiêu cho MỘT danh mục trong tuần hoặc tháng. Mỗi người có tối đa 1 hạn mức cho mỗi (danh mục, loại kỳ).</summary>
public class CategoryBudget
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public BudgetPeriod Period { get; set; }

    /// <summary>Hạn mức theo đồng (VND), luôn &gt; 0.</summary>
    public long Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
