using Spendly.Web.Common;

namespace Spendly.Web.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public bool IsAuthenticated => _accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string? UserId => IsAuthenticated ? _accessor.HttpContext!.User.GetUserId() : null;

    public string GetRequiredUserId() =>
        UserId ?? throw new InvalidOperationException("Người dùng chưa đăng nhập.");
}
