namespace Sellora.CoreService.Application.Hierarchy;

public sealed record HierarchyTreeResponse(
  Guid CompanyId,
  string Name,
  IReadOnlyList<ProvinceHierarchyNode> Provinces);

public sealed record ProvinceHierarchyNode(
  Guid ProvinceId,
  string Code,
  string Name,
  IReadOnlyList<AgencyHierarchyNode> Agencies,
  IReadOnlyList<TerritoryHierarchyNode> UnassignedTerritories);

public sealed record AgencyHierarchyNode(
  Guid AgencyId,
  string Name,
  IReadOnlyList<TerritoryHierarchyNode> Territories);

public sealed record TerritoryHierarchyNode(
  Guid TerritoryId,
  string Code,
  string Name,
  IReadOnlyList<ShopHierarchyNode> Shops);

public sealed record ShopHierarchyNode(
  Guid ShopId,
  string Name,
  string? OwnerName,
  string Address,
  decimal Latitude,
  decimal Longitude,
  decimal CreditLimit);