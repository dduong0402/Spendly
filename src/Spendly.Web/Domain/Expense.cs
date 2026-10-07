namespace Spendly.Web.Domain;

public class Expense
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>Số tiền theo đơn vị đồng (VND), luôn &gt; 0.</summary>
    public long Amount { get; set; }

    /// <summary>Ngày phát sinh khoản chi (giờ Việt Nam).</summary>
    public DateOnly SpentAt { get; set; }

    public string? Note { get; set; }

    /// <summary>Khác null nếu khoản chi này do một quy tắc định kỳ tự tạo.</summary>
    public int? RecurringExpenseId { get; set; }

    public RecurringExpense? RecurringExpense { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
