namespace Sellora.CoreService.Infrastructure.Persistence.Seeding;

/// <summary>
/// Stable identifiers for the committed development hierarchy.
/// Fixed IDs make the seed predictable for developers and automated tests.
/// </summary>
public static class DevelopmentOrganizationSeedIds
{
  public static readonly Guid CompanyId =
    Guid.Parse("30000000-0000-0000-0000-000000000001");

  public static readonly Guid CompanyAdminId =
    Guid.Parse("30000000-0000-0000-0000-000000000002");

  public static readonly Guid WesternProvinceId =
    Guid.Parse("30000000-0000-0000-0000-000000000010");

  public static readonly Guid CentralProvinceId =
    Guid.Parse("30000000-0000-0000-0000-000000000020");

  public static readonly Guid WesternManagerId =
    Guid.Parse("30000000-0000-0000-0000-000000000011");

  public static readonly Guid CentralManagerId =
    Guid.Parse("30000000-0000-0000-0000-000000000021");

  public static readonly Guid ColomboAgencyId =
    Guid.Parse("30000000-0000-0000-0000-000000000100");

  public static readonly Guid KandyAgencyId =
    Guid.Parse("30000000-0000-0000-0000-000000000200");

  public static readonly Guid ColomboOperatorId =
    Guid.Parse("30000000-0000-0000-0000-000000000101");

  public static readonly Guid KandyOperatorId =
    Guid.Parse("30000000-0000-0000-0000-000000000201");

  public static readonly Guid ColomboTerritoryId =
    Guid.Parse("30000000-0000-0000-0000-000000001000");

  public static readonly Guid KandyTerritoryId =
    Guid.Parse("30000000-0000-0000-0000-000000002000");

  public static readonly Guid ColomboSalesRepId =
    Guid.Parse("30000000-0000-0000-0000-000000001001");

  public static readonly Guid KandySalesRepId =
    Guid.Parse("30000000-0000-0000-0000-000000002001");

  public static readonly Guid ColomboShopId =
    Guid.Parse("30000000-0000-0000-0000-000000010000");

  public static readonly Guid KandyShopId =
    Guid.Parse("30000000-0000-0000-0000-000000020000");

  public static readonly Guid WesternManagerAssignmentId =
    Guid.Parse("30000000-0000-0000-0000-000000100010");

  public static readonly Guid CentralManagerAssignmentId =
    Guid.Parse("30000000-0000-0000-0000-000000100020");

  public static readonly Guid ColomboOperatorAssignmentId =
    Guid.Parse("30000000-0000-0000-0000-000000100100");

  public static readonly Guid KandyOperatorAssignmentId =
    Guid.Parse("30000000-0000-0000-0000-000000100200");

  public static readonly Guid ColomboAgencyAssignmentId =
    Guid.Parse("30000000-0000-0000-0000-000000101000");

  public static readonly Guid KandyAgencyAssignmentId =
    Guid.Parse("30000000-0000-0000-0000-000000102000");

  public static readonly Guid ColomboRepAssignmentId =
    Guid.Parse("30000000-0000-0000-0000-000000201000");

  public static readonly Guid KandyRepAssignmentId =
    Guid.Parse("30000000-0000-0000-0000-000000202000");
}