using System.Security.Claims;
using Spendly.Web.Auth;

namespace Spendly.Web.Common;

public static class SpendlyClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public static string GetDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirst(AppUserClaimsPrincipalFactory.DisplayNameClaim)?.Value
        ?? principal.Identity?.Name
        ?? string.Empty;
}
