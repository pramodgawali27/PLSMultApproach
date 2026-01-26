using System.Security.Claims;

namespace PLS.BuildingBlocks.Auth;

public static class ClaimsExtensions
{
    public const string TenantIdClaim = "tenant_id";
    public const string UserIdClaim = "user_id";

    public static Guid GetTenantId(this ClaimsPrincipal principal)
        => Guid.Parse(principal.FindFirstValue(TenantIdClaim) ?? throw new InvalidOperationException("Missing tenant_id claim."));

    public static Guid GetUserId(this ClaimsPrincipal principal)
        => Guid.Parse(principal.FindFirstValue(UserIdClaim) ?? throw new InvalidOperationException("Missing user_id claim."));
}
