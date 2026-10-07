using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Spendly.Web.Common;
using Spendly.Web.Domain;
using Spendly.Web.Models.Account;
using Spendly.Web.Services;

namespace Spendly.Web.Controllers;

/// <summary>Trang Tài khoản: thông tin, đổi mật khẩu, xuất dữ liệu, xóa tài khoản.</summary>
[Authorize]
public class ManageController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAccountDataService _data;
    private readonly IAccountDeletionService _deletion;
    private readonly TimeProvider _clock;
    private readonly ILogger<ManageController> _logger;

    public ManageController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAccountDataService data,
        IAccountDeletionService deletion,
        TimeProvider clock,
        ILogger<ManageController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _data = data;
        _deletion = deletion;
        _clock = clock;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        return View(ToIndexModel(user, new ProfileViewModel { DisplayName = user.DisplayName }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var name = model.DisplayName?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), ToIndexModel(user, model));
        }

        user.DisplayName = name;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault()?.Description ?? "Không thể lưu thay đổi.");
            return View(nameof(Index), ToIndexModel(user, model));
        }

        // Tên hiển thị nằm trong cookie đăng nhập (claim), cần phát hành lại để layout thấy tên mới.
        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Đã cập nhật tên hiển thị.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Đổi mật khẩu ----------

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.CurrentPassword == model.NewPassword)
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới phải khác mật khẩu hiện tại.");
            return View(model);
        }

        // Kiểm tra mật khẩu hiện tại có tính số lần sai (khóa tạm thời) để không bị dò mật khẩu qua trang này.
        var check = await _signInManager.CheckPasswordSignInAsync(user, model.CurrentPassword, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau vài phút.");
            return View(model);
        }

        if (!check.Succeeded)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
            return View(model);
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var description in result.Errors.Select(e => e.Description).Distinct())
            {
                ModelState.AddModelError(string.Empty, description);
            }

            return View(model);
        }

        // Giữ phiên hiện tại đăng nhập (security stamp mới); các thiết bị khác bị đăng xuất.
        await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("Người dùng {UserId} đã đổi mật khẩu.", user.Id);
        TempData["Success"] = "Đã đổi mật khẩu. Các thiết bị khác sẽ được đăng xuất.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Xuất dữ liệu ----------

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ExportCsv(CancellationToken cancellationToken)
    {
        var bytes = await _data.ExportCsvAsync(cancellationToken);
        return File(bytes, "text/csv; charset=utf-8", $"spendly-chi-tieu-{Today()}.csv");
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ExportJson(CancellationToken cancellationToken)
    {
        var bytes = await _data.ExportJsonAsync(cancellationToken);
        return File(bytes, "application/json; charset=utf-8", $"spendly-du-lieu-{Today()}.json");
    }

    // ---------- Xóa tài khoản ----------

    [HttpGet]
    public async Task<IActionResult> Delete()
    {
        ViewData["Summary"] = await _data.GetSummaryAsync();
        return View(new DeleteAccountViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(DeleteAccountViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (ModelState.IsValid)
        {
            var check = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (check.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau vài phút.");
            }
            else if (!check.Succeeded)
            {
                ModelState.AddModelError(nameof(model.Password), "Mật khẩu không đúng.");
            }
            else
            {
                var result = await _deletion.DeleteAccountAsync(user);
                if (result.IsSuccess)
                {
                    _logger.LogInformation("Đã xóa tài khoản {UserId}.", user.Id);
                    await _signInManager.SignOutAsync();
                    TempData["Success"] = "Đã xóa tài khoản và toàn bộ dữ liệu của bạn.";
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, result.Error ?? "Không thể xóa tài khoản. Vui lòng thử lại.");
            }
        }

        ViewData["Summary"] = await _data.GetSummaryAsync();
        return View(new DeleteAccountViewModel());
    }

    private string Today() => _clock.GetToday().ToString("yyyyMMdd");

    private static ManageIndexViewModel ToIndexModel(ApplicationUser user, ProfileViewModel profile) =>
        new(user.Email ?? string.Empty, user.CreatedAt == default ? null : user.CreatedAt, profile);
}
