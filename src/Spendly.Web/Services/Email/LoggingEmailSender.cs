namespace Spendly.Web.Services.Email;

/// <summary>
/// Dùng khi CHƯA cấu hình SMTP. Ở Development, in nội dung email (kể cả liên kết đặt lại mật khẩu) ra console để thử tính năng không cần mail thật.
/// Ngoài Development chỉ ghi cảnh báo, không bao giờ ghi nội dung email vào log.
/// </summary>
public sealed class LoggingEmailSender : IAppEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly IHostEnvironment _environment;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogWarning(
                "[EMAIL CHƯA CẤU HÌNH SMTP - chỉ in ra console ở Development]\nTới: {To}\nTiêu đề: {Subject}\n{Body}",
                message.To, message.Subject, message.TextBody);
        }
        else
        {
            _logger.LogError("Chưa cấu hình SMTP (mục Email) nên không gửi được email '{Subject}'.", message.Subject);
        }

        return Task.CompletedTask;
    }
}
