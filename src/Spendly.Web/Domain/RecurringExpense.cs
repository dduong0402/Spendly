namespace Spendly.Web.Domain;

public enum RecurrenceFrequency
{
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}

/// <summary>
/// Quy tắc khoản chi định kỳ (tiền nhà, internet, đăng ký dịch vụ...). Spendly tự tạo <see cref="Expense"/> mỗi khi đến hạn.
/// Ngày đến hạn luôn tính từ <see cref="StartDate"/> (neo), nên tháng ngắn không làm lệch các tháng sau.
/// </summary>
public class RecurringExpense
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    /// <summary>Tên hiển thị, cũng được ghi vào ghi chú của mỗi khoản chi được tạo (ví dụ "Tiền nhà").</summary>
    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>Số tiền mỗi lần, đơn vị đồng, luôn &gt; 0. Đổi số tiền chỉ ảnh hưởng các lần sau.</summary>
    public long Amount { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>Ngày đến hạn đầu tiên; ngày trong tuần/tháng/năm của các lần sau bám theo ngày này.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Ngày cuối cùng còn được tạo khoản chi (gồm cả ngày này); null = không kết thúc.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Lần đến hạn kế tiếp CHƯA được ghi.</summary>
    public DateOnly NextDueDate { get; set; }

    /// <summary>False = đang tạm dừng.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<Expense> Expenses { get; set; } = new();

    /// <summary>Đã qua ngày kết thúc: không còn lần nào để ghi.</summary>
    public bool IsFinished => EndDate is { } end && NextDueDate > end;
}
