using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Seeding;

/// <summary>
/// Creates a predictable development hierarchy covering two Sri Lankan
/// provinces. The operation is idempotent and is never run in production.
/// </summary>
public static class DevelopmentOrganizationSeeder
{
  private const string TenantCode = "SELLORA-DEMO";

  private static readonly DateTimeOffset SeededAt =
    new(
      year: 2026,
      month: 1,
      day: 1,
      hour: 0,
      minute: 0,
      second: 0,
      offset: TimeSpan.Zero);

  public static async Task SeedAsync(
    CoreDbContext db,
    CancellationToken cancellationToken = default)
  {
    var alreadySeeded = await db.Companies
      .IgnoreQueryFilters()
      .AnyAsync(
        company => company.TenantCode == TenantCode,
        cancellationToken);

    if (alreadySeeded)
    {
      return;
    }

    await using var transaction =
      await db.Database.BeginTransactionAsync(
        cancellationToken);

    db.AddRange(
      CreateCompany(),
      CreateCompanyAdmin(),
      CreateWesternProvince(),
      CreateCentralProvince(),
      CreateWesternManager(),
      CreateCentralManager(),
      CreateColomboAgency(),
      CreateKandyAgency(),
      CreateColomboOperator(),
      CreateKandyOperator(),
      CreateColomboTerritory(),
      CreateKandyTerritory(),
      CreateColomboSalesRep(),
      CreateKandySalesRep(),
      CreateWesternManagerAssignment(),
      CreateCentralManagerAssignment(),
      CreateColomboOperatorAssignment(),
      CreateKandyOperatorAssignment(),
      CreateColomboAgencyAssignment(),
      CreateKandyAgencyAssignment(),
      CreateColomboRepAssignment(),
      CreateKandyRepAssignment(),
      CreateColomboShop(),
      CreateKandyShop());

    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
  }

  private static Company CreateCompany()
  {
    return new Company
    {
      CompanyId = DevelopmentOrganizationSeedIds.CompanyId,
      TenantCode = TenantCode,
      Name = "Sellora Distribution Lanka",
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static StaffProfile CreateCompanyAdmin()
  {
    return CreateStaff(
      DevelopmentOrganizationSeedIds.CompanyAdminId,
      identitySub: "seed:company-admin",
      role: "CompanyAdmin",
      displayName: "Nimal Perera",
      email: "nimal.perera@sellora.local");
  }

  private static Province CreateWesternProvince()
  {
    return new Province
    {
      ProvinceId =
        DevelopmentOrganizationSeedIds.WesternProvinceId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      Code = "WP",
      Name = "Western Province",
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static Province CreateCentralProvince()
  {
    return new Province
    {
      ProvinceId =
        DevelopmentOrganizationSeedIds.CentralProvinceId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      Code = "CP",
      Name = "Central Province",
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static StaffProfile CreateWesternManager()
  {
    return CreateStaff(
      DevelopmentOrganizationSeedIds.WesternManagerId,
      identitySub: "seed:area-manager:western",
      role: "AreaManager",
      displayName: "Saman Fernando",
      email: "saman.fernando@sellora.local");
  }

  private static StaffProfile CreateCentralManager()
  {
    return CreateStaff(
      DevelopmentOrganizationSeedIds.CentralManagerId,
      identitySub: "seed:area-manager:central",
      role: "AreaManager",
      displayName: "Tharushi Silva",
      email: "tharushi.silva@sellora.local");
  }

  private static Agency CreateColomboAgency()
  {
    return new Agency
    {
      AgencyId =
        DevelopmentOrganizationSeedIds.ColomboAgencyId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      ProvinceId =
        DevelopmentOrganizationSeedIds.WesternProvinceId,
      Name = "Colombo Distribution Agency",
      Email = "colombo.agency@sellora.local",
      Phone = "+94 11 234 5678",
      Address = "120 Galle Road, Colombo 03",
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static Agency CreateKandyAgency()
  {
    return new Agency
    {
      AgencyId =
        DevelopmentOrganizationSeedIds.KandyAgencyId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      ProvinceId =
        DevelopmentOrganizationSeedIds.CentralProvinceId,
      Name = "Kandy Distribution Agency",
      Email = "kandy.agency@sellora.local",
      Phone = "+94 81 234 5678",
      Address = "45 Peradeniya Road, Kandy",
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static StaffProfile CreateColomboOperator()
  {
    return CreateStaff(
      DevelopmentOrganizationSeedIds.ColomboOperatorId,
      identitySub: "seed:agency-operator:colombo",
      role: "AgencyOperator",
      displayName: "Kasun Jayasinghe",
      email: "kasun.jayasinghe@sellora.local");
  }

  private static StaffProfile CreateKandyOperator()
  {
    return CreateStaff(
      DevelopmentOrganizationSeedIds.KandyOperatorId,
      identitySub: "seed:agency-operator:kandy",
      role: "AgencyOperator",
      displayName: "Dinithi Bandara",
      email: "dinithi.bandara@sellora.local");
  }

  private static Territory CreateColomboTerritory()
  {
    return new Territory
    {
      TerritoryId =
        DevelopmentOrganizationSeedIds.ColomboTerritoryId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      ProvinceId =
        DevelopmentOrganizationSeedIds.WesternProvinceId,
      Code = "WP-CMB-01",
      Name = "Colombo Central",
      GeographicDescription =
        "Colombo 01 through Colombo 07",
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static Territory CreateKandyTerritory()
  {
    return new Territory
    {
      TerritoryId =
        DevelopmentOrganizationSeedIds.KandyTerritoryId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      ProvinceId =
        DevelopmentOrganizationSeedIds.CentralProvinceId,
      Code = "CP-KDY-01",
      Name = "Kandy City",
      GeographicDescription =
        "Kandy municipal area and immediate suburbs",
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static StaffProfile CreateColomboSalesRep()
  {
    return CreateStaff(
      DevelopmentOrganizationSeedIds.ColomboSalesRepId,
      identitySub: "seed:sales-rep:colombo",
      role: "SalesRep",
      displayName: "Ruwan Dias",
      email: "ruwan.dias@sellora.local");
  }

  private static StaffProfile CreateKandySalesRep()
  {
    return CreateStaff(
      DevelopmentOrganizationSeedIds.KandySalesRepId,
      identitySub: "seed:sales-rep:kandy",
      role: "SalesRep",
      displayName: "Ishara Kumari",
      email: "ishara.kumari@sellora.local");
  }

  private static ProvinceManagerAssignment
    CreateWesternManagerAssignment()
  {
    return new ProvinceManagerAssignment
    {
      AssignmentId =
        DevelopmentOrganizationSeedIds
          .WesternManagerAssignmentId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      ProvinceId =
        DevelopmentOrganizationSeedIds.WesternProvinceId,
      AreaManagerId =
        DevelopmentOrganizationSeedIds.WesternManagerId,
      ReportsToAdminId =
        DevelopmentOrganizationSeedIds.CompanyAdminId,
      StartsAt = SeededAt,
      CreatedBy = "development-seed"
    };
  }

  private static ProvinceManagerAssignment
    CreateCentralManagerAssignment()
  {
    return new ProvinceManagerAssignment
    {
      AssignmentId =
        DevelopmentOrganizationSeedIds
          .CentralManagerAssignmentId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      ProvinceId =
        DevelopmentOrganizationSeedIds.CentralProvinceId,
      AreaManagerId =
        DevelopmentOrganizationSeedIds.CentralManagerId,
      ReportsToAdminId =
        DevelopmentOrganizationSeedIds.CompanyAdminId,
      StartsAt = SeededAt,
      CreatedBy = "development-seed"
    };
  }

  private static AgencyOperatorAssignment
    CreateColomboOperatorAssignment()
  {
    return new AgencyOperatorAssignment
    {
      AssignmentId =
        DevelopmentOrganizationSeedIds
          .ColomboOperatorAssignmentId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      AgencyId =
        DevelopmentOrganizationSeedIds.ColomboAgencyId,
      OperatorId =
        DevelopmentOrganizationSeedIds.ColomboOperatorId,
      StartsAt = SeededAt,
      CreatedBy = "development-seed"
    };
  }

  private static AgencyOperatorAssignment
    CreateKandyOperatorAssignment()
  {
    return new AgencyOperatorAssignment
    {
      AssignmentId =
        DevelopmentOrganizationSeedIds
          .KandyOperatorAssignmentId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      AgencyId =
        DevelopmentOrganizationSeedIds.KandyAgencyId,
      OperatorId =
        DevelopmentOrganizationSeedIds.KandyOperatorId,
      StartsAt = SeededAt,
      CreatedBy = "development-seed"
    };
  }

  private static TerritoryAgencyAssignment
    CreateColomboAgencyAssignment()
  {
    return new TerritoryAgencyAssignment
    {
      AssignmentId =
        DevelopmentOrganizationSeedIds
          .ColomboAgencyAssignmentId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      TerritoryId =
        DevelopmentOrganizationSeedIds.ColomboTerritoryId,
      AgencyId =
        DevelopmentOrganizationSeedIds.ColomboAgencyId,
      StartsAt = SeededAt,
      CreatedBy = "development-seed"
    };
  }

  private static TerritoryAgencyAssignment
    CreateKandyAgencyAssignment()
  {
    return new TerritoryAgencyAssignment
    {
      AssignmentId =
        DevelopmentOrganizationSeedIds
          .KandyAgencyAssignmentId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      TerritoryId =
        DevelopmentOrganizationSeedIds.KandyTerritoryId,
      AgencyId =
        DevelopmentOrganizationSeedIds.KandyAgencyId,
      StartsAt = SeededAt,
      CreatedBy = "development-seed"
    };
  }

  private static SalesRepTerritoryAssignment
    CreateColomboRepAssignment()
  {
    return new SalesRepTerritoryAssignment
    {
      AssignmentId =
        DevelopmentOrganizationSeedIds
          .ColomboRepAssignmentId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      TerritoryId =
        DevelopmentOrganizationSeedIds.ColomboTerritoryId,
      SalesRepId =
        DevelopmentOrganizationSeedIds.ColomboSalesRepId,
      StartsAt = SeededAt,
      CreatedBy = "development-seed"
    };
  }

  private static SalesRepTerritoryAssignment
    CreateKandyRepAssignment()
  {
    return new SalesRepTerritoryAssignment
    {
      AssignmentId =
        DevelopmentOrganizationSeedIds
          .KandyRepAssignmentId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      TerritoryId =
        DevelopmentOrganizationSeedIds.KandyTerritoryId,
      SalesRepId =
        DevelopmentOrganizationSeedIds.KandySalesRepId,
      StartsAt = SeededAt,
      CreatedBy = "development-seed"
    };
  }

  private static Shop CreateColomboShop()
  {
    return new Shop
    {
      ShopId =
        DevelopmentOrganizationSeedIds.ColomboShopId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      TerritoryId =
        DevelopmentOrganizationSeedIds.ColomboTerritoryId,
      Name = "Lake View Mini Mart",
      OwnerName = "Chaminda Perera",
      OwnerIdentitySub = "seed:shop-owner:colombo",
      OwnerEmail = "chaminda@example.test",
      OwnerPhone = "+94 77 123 4567",
      Address = "18 Sir James Pieris Mawatha, Colombo 02",
      Latitude = 6.918900m,
      Longitude = 79.856400m,
      CreditLimit = 250000.00m,
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static Shop CreateKandyShop()
  {
    return new Shop
    {
      ShopId =
        DevelopmentOrganizationSeedIds.KandyShopId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      TerritoryId =
        DevelopmentOrganizationSeedIds.KandyTerritoryId,
      Name = "Hill Capital Stores",
      OwnerName = "Nadeesha Bandara",
      OwnerIdentitySub = "seed:shop-owner:kandy",
      OwnerEmail = "nadeesha@example.test",
      OwnerPhone = "+94 76 234 5678",
      Address = "72 D. S. Senanayake Veediya, Kandy",
      Latitude = 7.290600m,
      Longitude = 80.633700m,
      CreditLimit = 175000.00m,
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }

  private static StaffProfile CreateStaff(
    Guid staffProfileId,
    string identitySub,
    string role,
    string displayName,
    string email)
  {
    return new StaffProfile
    {
      StaffProfileId = staffProfileId,
      CompanyId =
        DevelopmentOrganizationSeedIds.CompanyId,
      IdentitySub = identitySub,
      Role = role,
      DisplayName = displayName,
      Email = email,
      Status = HierarchyStatus.Active,
      CreatedAt = SeededAt
    };
  }
}