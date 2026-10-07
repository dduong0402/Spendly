using Spendly.Web.Services;

namespace Spendly.Tests.TestSupport;

public class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; set; }

    public bool IsAuthenticated => UserId is not null;

    public string GetRequiredUserId() => UserId ?? throw new InvalidOperationException("Chưa đăng nhập.");
}
