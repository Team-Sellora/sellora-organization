using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public static class HierarchyEndpointTestData
{
  public static readonly Guid CompanyId =
    Guid.Parse("10000000-0000-0000-0000-000000000001");

  public static readonly Guid OtherCompanyId =
    Guid.Parse("20000000-0000-0000-0000-000000000001");

  public static readonly Guid CompanyAdminId =
    Guid.Parse("10000000-0000-0000-0000-000000000002");

  public static readonly Guid NorthProvinceId =
    Guid.Parse("10000000-0000-0000-0000-000000000010");

  public static readonly Guid SouthProvinceId =
    Guid.Parse("10000000-0000-0000-0000-000000000020");

  public static readonly Guid OtherProvinceId =
    Guid.Parse("20000000-0000-0000-0000-000000000010");

  public static readonly Guid NorthAgencyId =
    Guid.Parse("10000000-0000-0000-0000-000000000100");

  public static readonly Guid SouthAgencyId =
    Guid.Parse("10000000-0000-0000-0000-000000000200");

  public static readonly Guid NorthTerritoryId =
    Guid.Parse("10000000-0000-0000-0000-000000001000");

  public static readonly Guid SouthTerritoryId =
    Guid.Parse("10000000-0000-0000-0000-000000002000");

  public static readonly Guid NorthShopId =
    Guid.Parse("10000000-0000-0000-0000-000000010000");

  public static readonly Guid NorthOtherShopId =
    Guid.Parse("10000000-0000-0000-0000-000000020000");

  public static readonly Guid SouthShopId =
    Guid.Parse("10000000-0000-0000-0000-000000030000");

  public const string AreaManagerSubject = "area-manager-north";
  public const string CompanyAdminSubject = "test-admin";
  public const string AgencyOperatorSubject = "operator-north";
  public const string SalesRepSubject = "sales-rep-north";
  public const string ShopOwnerSubject = "shop-owner-north";

  public static void Seed(CoreDbContext db)
  {
    if (db.Companies
      .IgnoreQueryFilters()
      .Any(company => company.CompanyId == CompanyId))
    {
      return;
    }

    var now = DateTimeOffset.UtcNow;

    var northManagerId = Guid.NewGuid();
    var southManagerId = Guid.NewGuid();
    var northOperatorId = Guid.NewGuid();
    var southOperatorId = Guid.NewGuid();
    var northRepId = Guid.NewGuid();
    var southRepId = Guid.NewGuid();

    db.AddRange(
      new Company
      {
        CompanyId = CompanyId,
        TenantCode = "TEST-COMPANY-A",
        Name = "Test Company A",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Company
      {
        CompanyId = OtherCompanyId,
        TenantCode = "TEST-COMPANY-B",
        Name = "Test Company B",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Province
      {
        ProvinceId = NorthProvinceId,
        CompanyId = CompanyId,
        Code = "NORTH",
        Name = "North Province",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Province
      {
        ProvinceId = SouthProvinceId,
        CompanyId = CompanyId,
        Code = "SOUTH",
        Name = "South Province",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Province
      {
        ProvinceId = OtherProvinceId,
        CompanyId = OtherCompanyId,
        Code = "OTHER",
        Name = "Other Company Province",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      CreateStaff(
        CompanyAdminId,
        CompanyId,
        CompanyAdminSubject,
        "CompanyAdmin"),
      CreateStaff(
        northManagerId,
        CompanyId,
        AreaManagerSubject,
        "AreaManager"),
      CreateStaff(
        southManagerId,
        CompanyId,
        "area-manager-south",
        "AreaManager"),
      CreateStaff(
        northOperatorId,
        CompanyId,
        AgencyOperatorSubject,
        "AgencyOperator"),
      CreateStaff(
        southOperatorId,
        CompanyId,
        "operator-south",
        "AgencyOperator"),
      CreateStaff(
        northRepId,
        CompanyId,
        SalesRepSubject,
        "SalesRep"),
      CreateStaff(
        southRepId,
        CompanyId,
        "sales-rep-south",
        "SalesRep"),
      new Agency
      {
        AgencyId = NorthAgencyId,
        CompanyId = CompanyId,
        ProvinceId = NorthProvinceId,
        Name = "North Agency",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Agency
      {
        AgencyId = SouthAgencyId,
        CompanyId = CompanyId,
        ProvinceId = SouthProvinceId,
        Name = "South Agency",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Territory
      {
        TerritoryId = NorthTerritoryId,
        CompanyId = CompanyId,
        ProvinceId = NorthProvinceId,
        Code = "N-01",
        Name = "North Territory",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Territory
      {
        TerritoryId = SouthTerritoryId,
        CompanyId = CompanyId,
        ProvinceId = SouthProvinceId,
        Code = "S-01",
        Name = "South Territory",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new ProvinceManagerAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = CompanyId,
        ProvinceId = NorthProvinceId,
        AreaManagerId = northManagerId,
        ReportsToAdminId = CompanyAdminId,
        StartsAt = now,
        CreatedBy = "test-seed"
      },
      new ProvinceManagerAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = CompanyId,
        ProvinceId = SouthProvinceId,
        AreaManagerId = southManagerId,
        ReportsToAdminId = CompanyAdminId,
        StartsAt = now,
        CreatedBy = "test-seed"
      },
      new AgencyOperatorAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = CompanyId,
        AgencyId = NorthAgencyId,
        OperatorId = northOperatorId,
        StartsAt = now,
        CreatedBy = "test-seed"
      },
      new AgencyOperatorAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = CompanyId,
        AgencyId = SouthAgencyId,
        OperatorId = southOperatorId,
        StartsAt = now,
        CreatedBy = "test-seed"
      },
      new TerritoryAgencyAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = CompanyId,
        TerritoryId = NorthTerritoryId,
        AgencyId = NorthAgencyId,
        StartsAt = now,
        CreatedBy = "test-seed"
      },
      new TerritoryAgencyAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = CompanyId,
        TerritoryId = SouthTerritoryId,
        AgencyId = SouthAgencyId,
        StartsAt = now,
        CreatedBy = "test-seed"
      },
      new SalesRepTerritoryAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = CompanyId,
        TerritoryId = NorthTerritoryId,
        SalesRepId = northRepId,
        StartsAt = now,
        CreatedBy = "test-seed"
      },
      new SalesRepTerritoryAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = CompanyId,
        TerritoryId = SouthTerritoryId,
        SalesRepId = southRepId,
        StartsAt = now,
        CreatedBy = "test-seed"
      },
      CreateShop(
        NorthShopId,
        CompanyId,
        NorthTerritoryId,
        "North Owner Shop",
        ShopOwnerSubject),
      CreateShop(
        NorthOtherShopId,
        CompanyId,
        NorthTerritoryId,
        "North Other Shop",
        "other-shop-owner"),
      CreateShop(
        SouthShopId,
        CompanyId,
        SouthTerritoryId,
        "South Shop",
        "south-shop-owner"));

    db.SaveChanges();
  }

  private static StaffProfile CreateStaff(
    Guid id,
    Guid companyId,
    string subject,
    string role)
  {
    return new StaffProfile
    {
      StaffProfileId = id,
      CompanyId = companyId,
      IdentitySub = subject,
      Role = role,
      DisplayName = subject,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };
  }

  private static Shop CreateShop(
    Guid id,
    Guid companyId,
    Guid territoryId,
    string name,
    string ownerSubject)
  {
    return new Shop
    {
      ShopId = id,
      CompanyId = companyId,
      TerritoryId = territoryId,
      Name = name,
      OwnerName = name,
      OwnerIdentitySub = ownerSubject,
      Address = "123 Test Road",
      Latitude = 6.927079m,
      Longitude = 79.861244m,
      CreditLimit = 10000m,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };
  }
}
