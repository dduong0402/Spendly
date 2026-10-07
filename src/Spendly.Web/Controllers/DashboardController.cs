using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Spendly.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index() => View();
}
