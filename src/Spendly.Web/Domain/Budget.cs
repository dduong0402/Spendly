namespace Spendly.Web.Domain;

public enum BudgetPeriod
{
    Week = 1,
    Month = 2
}

/// <summary>Hạn mức chi tiêu tổng của một người dùng cho tuần hoặc tháng. Mỗi người có tối đa 1 hạn mức tuần và 1 hạn mức tháng.</summary>
public class Budget
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public BudgetPeriod Period { get; set; }

    /// <summary>Hạn mức theo đồng (VND), luôn &gt; 0.</summary>
    public long Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
