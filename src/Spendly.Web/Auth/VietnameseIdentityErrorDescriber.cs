using Microsoft.AspNetCore.Identity;

namespace Spendly.Web.Auth;

/// <summary>Việt hóa thông báo lỗi mặc định của ASP.NET Core Identity.</summary>
public class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError Describe(string code, string description) =>
        new() { Code = code, Description = description };

    public override IdentityError DefaultError() =>
        Describe(nameof(DefaultError), "Đã có lỗi xảy ra. Vui lòng thử lại.");

    public override IdentityError InvalidToken() =>
        Describe(nameof(InvalidToken), "Liên kết không hợp lệ hoặc đã hết hạn. Hãy yêu cầu liên kết mới.");

    public override IdentityError DuplicateUserName(string userName) =>
        Describe(nameof(DuplicateUserName), "Email này đã được đăng ký.");

    public override IdentityError DuplicateEmail(string email) =>
        Describe(nameof(DuplicateEmail), "Email này đã được đăng ký.");

    public override IdentityError InvalidUserName(string? userName) =>
        Describe(nameof(InvalidUserName), "Email không hợp lệ.");

    public override IdentityError InvalidEmail(string? email) =>
        Describe(nameof(InvalidEmail), "Email không hợp lệ.");

    public override IdentityError PasswordMismatch() =>
        Describe(nameof(PasswordMismatch), "Mật khẩu hiện tại không đúng.");

    public override IdentityError PasswordTooShort(int length) =>
        Describe(nameof(PasswordTooShort), $"Mật khẩu phải có ít nhất {length} ký tự.");

    public override IdentityError PasswordRequiresDigit() =>
        Describe(nameof(PasswordRequiresDigit), "Mật khẩu phải có ít nhất một chữ số (0-9).");

    public override IdentityError PasswordRequiresLower() =>
        Describe(nameof(PasswordRequiresLower), "Mật khẩu phải có ít nhất một chữ thường (a-z).");

    public override IdentityError PasswordRequiresUpper() =>
        Describe(nameof(PasswordRequiresUpper), "Mật khẩu phải có ít nhất một chữ hoa (A-Z).");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Describe(nameof(PasswordRequiresNonAlphanumeric), "Mật khẩu phải có ít nhất một ký tự đặc biệt.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Describe(nameof(PasswordRequiresUniqueChars), $"Mật khẩu phải có ít nhất {uniqueChars} ký tự khác nhau.");
}
