using Microsoft.AspNetCore.Authorization;

namespace Sellora.CoreService.Api.Authorization;

// Central definition of the five Sellora role authorization policies.
// Endpoints declare a required role via [Authorize(Policy = RolePolicies.RequireSalesRep)]
// rather than each writing its own role-checking logic.
// Policies read the role from the JWT role claim — never a database lookup.
public static class RolePolicies
{
    public const string RequireCompanyAdmin = "RequireCompanyAdmin";
    public const string RequireAreaManager = "RequireAreaManager";
    public const string RequireAgencyOperator = "RequireAgencyOperator";
    public const string RequireSalesRep = "RequireSalesRep";
    public const string RequireShopOwner = "RequireShopOwner";

    public static void AddSelloraRolePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireCompanyAdmin, p => p.RequireRole("CompanyAdmin"));
        options.AddPolicy(RequireAreaManager, p => p.RequireRole("AreaManager"));
        options.AddPolicy(RequireAgencyOperator, p => p.RequireRole("AgencyOperator"));
        options.AddPolicy(RequireSalesRep, p => p.RequireRole("SalesRep"));
        options.AddPolicy(RequireShopOwner, p => p.RequireRole("ShopOwner"));
    }
}