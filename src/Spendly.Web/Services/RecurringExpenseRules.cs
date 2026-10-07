using Spendly.Web.Domain;
using Spendly.Web.Models.Recurring;

namespace Spendly.Web.Services;

public static class RecurringExpenseRules
{
    public const int MaxNameLength = 100;

    /// <summary>Cho phép bắt đầu trong quá khứ tối đa chừng này ngày (các lần đã qua sẽ được ghi bù).</summary>
    public const int MaxPastDays = 366;

    public const int MaxFutureDays = 366;

    /// <summary>Số khoản chi tối đa tạo trong một lần chạy, để một quy tắc cũ không tạo hàng loạt cùng lúc; phần còn lại ghi ở lần sau.</summary>
    public const int MaxGeneratedPerRun = 500;

    /// <summary>Trả về thông báo lỗi, hoặc null nếu hợp lệ.</summary>
    public static string? Validate(SaveRecurringRequest request, DateOnly today)
    {
        var common = ValidateCommon(request.Name, request.Amount);
        if (common is not null)
        {
            return common;
        }

        if (!Enum.IsDefined(request.Frequency))
        {
            return "Chu kỳ không hợp lệ.";
        }

        if (request.StartDate < today.AddDays(-MaxPastDays))
        {
            return "Ngày bắt đầu không được sớm hơn 1 năm trước.";
        }

        if (request.StartDate > today.AddDays(MaxFutureDays))
        {
            return "Ngày bắt đầu không được xa hơn 1 năm tới.";
        }

        return ValidateEnd(request.StartDate, request.EndDate);
    }

    public static string? Validate(UpdateRecurringRequest request, DateOnly startDate)
    {
        return ValidateCommon(request.Name, request.Amount) ?? ValidateEnd(startDate, request.EndDate);
    }

    private static string? ValidateCommon(string? name, long amount)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Vui lòng nhập tên khoản chi.";
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return $"Tên tối đa {MaxNameLength} ký tự.";
        }

        if (amount <= 0)
        {
            return "Số tiền phải lớn hơn 0.";
        }

        if (amount > ExpenseRules.MaxAmount)
        {
            return "Số tiền quá lớn.";
        }

        return null;
    }

    private static string? ValidateEnd(DateOnly start, DateOnly? end)
    {
        if (end is { } endDate && endDate < start)
        {
            return "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.";
        }

        return null;
    }
}
