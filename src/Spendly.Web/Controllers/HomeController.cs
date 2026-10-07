using Microsoft.AspNetCore.Mvc;

namespace Spendly.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // Đã đăng nhập thì vào thẳng Dashboard, trang chủ dành cho khách.
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View();
    }

    public IActionResult Error() => View();

    /// <summary>Trang thân thiện cho các mã lỗi HTTP (404, 403...) do UseStatusCodePagesWithReExecute gọi tới.</summary>
    public IActionResult HttpStatus(int id) => View(id);
}
