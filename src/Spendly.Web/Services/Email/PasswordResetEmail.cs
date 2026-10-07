using System.Net;

namespace Spendly.Web.Services.Email;

public static class PasswordResetEmail
{
    public const string Subject = "Đặt lại mật khẩu Spendly";

    public static EmailMessage Build(string to, string displayName, string link, int validMinutes)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "bạn" : displayName.Trim();

        var text =
            $"Xin chào {name},\n\n" +
            "Spendly nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Mở liên kết sau để đặt mật khẩu mới:\n\n" +
            $"{link}\n\n" +
            $"Liên kết có hiệu lực trong {validMinutes} phút và chỉ dùng được một lần.\n" +
            "Nếu bạn không yêu cầu, hãy bỏ qua email này. Mật khẩu của bạn vẫn giữ nguyên.\n\n" +
            "Spendly";

        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;color:#0B1B33;max-width:480px;margin:0 auto;padding:24px\">" +
            $"<p>Xin chào {WebUtility.HtmlEncode(name)},</p>" +
            "<p>Spendly nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>" +
            $"<p style=\"margin:28px 0\"><a href=\"{WebUtility.HtmlEncode(link)}\" " +
            "style=\"background:#1F6BFF;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:999px;font-weight:600;display:inline-block\">" +
            "Đặt lại mật khẩu</a></p>" +
            $"<p style=\"font-size:14px\">Liên kết có hiệu lực trong {validMinutes} phút và chỉ dùng được một lần.</p>" +
            "<p style=\"font-size:14px\">Nếu nút không bấm được, sao chép liên kết này vào trình duyệt:<br />" +
            $"<span style=\"word-break:break-all\">{WebUtility.HtmlEncode(link)}</span></p>" +
            "<p style=\"font-size:14px;color:#5B6B82\">Nếu bạn không yêu cầu, hãy bỏ qua email này. Mật khẩu của bạn vẫn giữ nguyên.</p>" +
            "</div>";

        return new EmailMessage(to, Subject, html, text);
    }
}
