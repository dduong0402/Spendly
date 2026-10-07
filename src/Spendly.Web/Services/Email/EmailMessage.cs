namespace Spendly.Web.Services.Email;

/// <summary>Một email gửi đi: có cả bản HTML và bản chữ thường (cho trình đọc mail không hiển thị HTML).</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);
