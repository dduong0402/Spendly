using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Spendly.Web.Domain;

namespace Spendly.Web.Auth;

/// <summary>Thêm claim "display_name" vào cookie đăng nhập để layout hiển thị tên mà không cần truy vấn DB.</summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string DisplayNameClaim = "display_name";

    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(DisplayNameClaim, user.DisplayName));
        return identity;
    }
}
