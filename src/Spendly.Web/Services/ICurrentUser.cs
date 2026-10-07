namespace Spendly.Web.Services;

/// <summary>Người dùng đang đăng nhập. Mọi truy vấn dữ liệu phải dùng UserId từ đây, không lấy từ form/query.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    /// <summary>Trả về UserId hoặc ném lỗi nếu chưa đăng nhập.</summary>
    string GetRequiredUserId();
}
