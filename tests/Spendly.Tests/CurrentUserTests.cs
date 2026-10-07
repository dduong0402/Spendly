using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class CurrentUserTests
{
    [Fact]
    public void WithoutHttpContext_IsNotAuthenticated()
    {
        var current = new CurrentUser(new HttpContextAccessor());

        current.IsAuthenticated.Should().BeFalse();
        current.UserId.Should().BeNull();
        var act = () => current.GetRequiredUserId();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WithAuthenticatedPrincipal_ReturnsUserId()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }, "test");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        var current = new CurrentUser(accessor);

        current.IsAuthenticated.Should().BeTrue();
        current.UserId.Should().Be("user-1");
        current.GetRequiredUserId().Should().Be("user-1");
    }

    [Fact]
    public void WithAnonymousPrincipal_IsNotAuthenticated()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var current = new CurrentUser(accessor);

        current.IsAuthenticated.Should().BeFalse();
        current.UserId.Should().BeNull();
    }
}
