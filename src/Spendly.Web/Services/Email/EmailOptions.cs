namespace Spendly.Web.Services.Email;

/// <summary>
/// Cấu hình gửi email (mục "Email" trong appsettings). KHÔNG đặt Password vào appsettings.json:
/// dùng User Secrets khi phát triển (<c>dotnet user-secrets set "Email:Password" "..."</c>) và biến môi trường khi triển khai (Email__Password).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>Địa chỉ người gửi; bỏ trống thì dùng UserName.</summary>
    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "Spendly";

    public bool EnableSsl { get; set; } = true;

    /// <summary>Có Host nghĩa là đã cấu hình SMTP.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    public string EffectiveFromAddress => string.IsNullOrWhiteSpace(FromAddress) ? UserName ?? string.Empty : FromAddress;
}

/// <summary>Cấu hình chung của ứng dụng (mục "App").</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Địa chỉ công khai của website (ví dụ https://spendly.example.com), dùng để dựng liên kết trong email.
    /// Bỏ trống thì lấy theo request (chỉ nên như vậy khi chạy local; môi trường thật phải đặt để tránh giả mạo header Host).
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
