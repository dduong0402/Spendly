using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Options;

namespace Spendly.Web.Services.Email;

/// <summary>Gửi email qua SMTP (ví dụ Gmail: smtp.gmail.com, cổng 587, dùng mật khẩu ứng dụng).</summary>
public sealed class SmtpEmailSender : IAppEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var from = _options.EffectiveFromAddress;
        if (string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException("Thiếu Email:FromAddress (hoặc Email:UserName) trong cấu hình.");
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(from, _options.FromName, Encoding.UTF8),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };

        mail.To.Add(new MailAddress(message.To));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15_000
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }
}
