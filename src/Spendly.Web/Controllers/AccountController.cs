using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Spendly.Web.Auth;
using Spendly.Web.Domain;
using Spendly.Web.Models.Account;
using Spendly.Web.Services.Email;

namespace Spendly.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAppEmailSender _email;
    private readonly AppOptions _appOptions;
    private readonly ILogger<AccountController> _logger;



    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAppEmailSender email,
        IOptions<AppOptions> appOptions,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _email = email;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = model.DisplayName.Trim()
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            // Identity có thể trả trùng lỗi (DuplicateUserName + DuplicateEmail cùng nội dung).
            foreach (var description in result.Errors.Select(e => e.Description).Distinct())
            {
                ModelState.AddModelError(string.Empty, description);
            }

            return View(model);
        }

        _logger.LogInformation("Người dùng mới đăng ký: {UserId}", user.Id);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email.Trim(),
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        ModelState.AddModelError(
            string.Empty,
            result.IsLockedOut
                ? "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau vài phút."
                : "Email hoặc mật khẩu không đúng.");

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ---------- Quên / đặt lại mật khẩu ----------

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("ChangePassword", "Manage");
        }

        return View(new ForgotPasswordViewModel());
    }

    /// <summary>
    /// Luôn trả về cùng một kết quả dù email có tồn tại hay không (không để lộ email nào đã đăng ký).
    /// Giới hạn tốc độ theo IP để không bị dùng làm công cụ gửi thư rác.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null)
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var link = BuildAbsoluteUrl(Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, token }));
                var minutes = (int)Spendly.Web.Auth.IdentityServiceCollectionExtensions.PasswordResetLifetime.TotalMinutes;
                await _email.SendAsync(PasswordResetEmail.Build(user.Email!, user.DisplayName, link, minutes), HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Lỗi gửi mail không được lộ ra ngoài: nếu không, kẻ xấu phân biệt được email nào có tài khoản.
                _logger.LogError(ex, "Không gửi được email đặt lại mật khẩu cho người dùng {UserId}.", user.Id);
            }
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return View("ResetPasswordInvalid");
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            // Không tiết lộ email không tồn tại: trả về như thể đã thành công.
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var description in result.Errors.Select(e => e.Description).Distinct())
            {
                ModelState.AddModelError(string.Empty, description);
            }

            return View(model);
        }

        // Người dùng đã chứng minh quyền truy cập email: gỡ khóa đăng nhập (nếu có) để họ đăng nhập được ngay.
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        _logger.LogInformation("Người dùng {UserId} đã đặt lại mật khẩu.", user.Id);
        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : RedirectToAction("Index", "Dashboard");

    /// <summary>
    /// Dựng liên kết đầy đủ cho email. Có App:PublicBaseUrl thì dùng nó (an toàn trước giả mạo header Host),
    /// không thì lấy theo request hiện tại (chỉ nên dùng khi chạy local).
    /// </summary>
    private string BuildAbsoluteUrl(string? path)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_appOptions.PublicBaseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : _appOptions.PublicBaseUrl.TrimEnd('/');

        return baseUrl + path;
    }
}
