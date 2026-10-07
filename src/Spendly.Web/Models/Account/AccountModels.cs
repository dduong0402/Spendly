using System.ComponentModel.DataAnnotations;

namespace Spendly.Web.Models.Account;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required(ErrorMessage = "Liên kết không hợp lệ.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Liên kết không hợp lệ.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có từ 8 đến 100 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có từ 8 đến 100 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
    [StringLength(100, ErrorMessage = "Tên hiển thị tối đa 100 ký tự.")]
    [Display(Name = "Tên hiển thị")]
    public string DisplayName { get; set; } = string.Empty;
}

public class DeleteAccountViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu để xác nhận.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;
}

public sealed record ManageIndexViewModel(string Email, DateTimeOffset? MemberSince, ProfileViewModel Profile);

/// <summary>Số liệu dữ liệu của người dùng, hiển thị ở trang xóa tài khoản.</summary>
public sealed record AccountDataSummary(int Expenses, int Categories, int RecurringExpenses, int Budgets);
