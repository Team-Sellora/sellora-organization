using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Sellora.CoreService.Api.Authorization;

public static class RolePolicies
{
    public const string RequireCompanyAdmin = "RequireCompanyAdmin";
    public const string RequireAreaManager = "RequireAreaManager";
    public const string RequireAgencyOperator = "RequireAgencyOperator";
    public const string RequireSalesRep = "RequireSalesRep";
    public const string RequireShopOwner = "RequireShopOwner";
    public const string RequireHierarchyReader = "RequireHierarchyReader";

    public static void AddSelloraRolePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireCompanyAdmin, p => p.RequireAssertion(HasRole("CompanyAdmin")));
        options.AddPolicy(RequireAreaManager, p => p.RequireAssertion(HasRole("AreaManager")));
        options.AddPolicy(RequireAgencyOperator, p => p.RequireAssertion(HasRole("AgencyOperator")));
        options.AddPolicy(RequireSalesRep, p => p.RequireAssertion(HasRole("SalesRep")));
        options.AddPolicy(RequireShopOwner, p => p.RequireAssertion(HasRole("ShopOwner")));
        options.AddPolicy(RequireHierarchyReader, p => p.RequireAssertion(HasAnyRole(
            "CompanyAdmin", "AreaManager", "AgencyOperator", "SalesRep", "ShopOwner")));
    }

    // WSO2 IS emits the "roles" claim, but .NET's default JWT handler renames it
    // to ClaimTypes.Role (http://schemas.microsoft.com/ws/2008/06/identity/claims/role)
    // before we see it. Match on the actually-received claim type, not "roles".
    private static Func<AuthorizationHandlerContext, bool> HasRole(string role) =>
        ctx => ctx.User.HasClaim(ClaimTypes.Role, role);

    private static Func<AuthorizationHandlerContext, bool> HasAnyRole(params string[] roles) =>
        ctx => roles.Any(r => ctx.User.HasClaim(ClaimTypes.Role, r));
}