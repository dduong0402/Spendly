namespace Spendly.Web.Services.Email;

public interface IAppEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
