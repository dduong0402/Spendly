using Spendly.Web.Models.Expenses;

namespace Spendly.Web.Services;

public static class ExpenseRules
{
    public const long MaxAmount = 100_000_000_000; // 100 tỷ đồng
    public const int MaxNoteLength = 500;
    public static readonly DateOnly MinDate = new(2000, 1, 1);

    /// <summary>Trả về thông báo lỗi, hoặc null nếu hợp lệ.</summary>
    public static string? Validate(SaveExpenseRequest request, DateOnly today)
    {
        if (request.Amount <= 0)
        {
            return "Số tiền phải lớn hơn 0.";
        }

        if (request.Amount > MaxAmount)
        {
            return "Số tiền quá lớn.";
        }

        if (request.SpentAt > today)
        {
            return "Ngày chi không được ở tương lai.";
        }

        if (request.SpentAt < MinDate)
        {
            return "Ngày chi không hợp lệ.";
        }

        if (request.Note is { Length: > MaxNoteLength })
        {
            return $"Ghi chú tối đa {MaxNoteLength} ký tự.";
        }

        return null;
    }
}
