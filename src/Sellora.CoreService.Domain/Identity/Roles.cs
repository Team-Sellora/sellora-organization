namespace Sellora.CoreService.Domain.Identity;

/// <summary>
/// The five Organisation-audience role names defined in WSO2 Identity Server.
/// Single source of truth for role strings stored in staff_profile.role and
/// carried in the token's role claim, so a rename never drifts between the
/// authorization layer and the persisted profile.
/// </summary>
public static class Roles
{
  public const string CompanyAdmin = "CompanyAdmin";
  public const string AreaManager = "AreaManager";
  public const string AgencyOperator = "AgencyOperator";
  public const string SalesRep = "SalesRep";
  public const string ShopOwner = "ShopOwner";
}