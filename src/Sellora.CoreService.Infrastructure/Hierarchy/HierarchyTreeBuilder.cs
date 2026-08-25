using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Hierarchy;

/// <summary>
/// Converts flat database rows into the nested structure returned to the
/// frontend. No database access or authorization decisions occur here.
/// </summary>
internal static class HierarchyTreeBuilder
{
  public static HierarchyTreeResponse Build(
    Company company,
    HierarchyDataSet data)
  {
    var shopsByTerritory = BuildShopLookup(data.Shops);

    var territoryNodes = data.Territories
      .ToDictionary(
        territory => territory.TerritoryId,
        territory => CreateTerritoryNode(
          territory,
          shopsByTerritory));

    var agencyIdByTerritory =
      data.AgencyAssignments.ToDictionary(
        assignment => assignment.TerritoryId,
        assignment => assignment.AgencyId);

    var territoriesByAgency =
      BuildAgencyTerritoryLookup(
        data.AgencyAssignments,
        territoryNodes);

    var agenciesByProvince =
      BuildProvinceAgencyLookup(
        data.Agencies,
        territoriesByAgency);

    var unassignedByProvince =
      BuildUnassignedTerritoryLookup(
        data.Territories,
        territoryNodes,
        agencyIdByTerritory);

    var provinceNodes = data.Provinces
      .OrderBy(province => province.Name)
      .Select(province => CreateProvinceNode(
        province,
        agenciesByProvince,
        unassignedByProvince))
      .ToList();

    return new HierarchyTreeResponse(
      company.CompanyId,
      company.Name,
      provinceNodes);
  }

  private static Dictionary<Guid, IReadOnlyList<ShopHierarchyNode>>
    BuildShopLookup(IEnumerable<Shop> shops)
  {
    return shops
      .GroupBy(shop => shop.TerritoryId)
      .ToDictionary(
        group => group.Key,
        group =>
          (IReadOnlyList<ShopHierarchyNode>)group
            .OrderBy(shop => shop.Name)
            .Select(shop => new ShopHierarchyNode(
              shop.ShopId,
              shop.Name,
              shop.OwnerName,
              shop.Address,
              shop.Latitude,
              shop.Longitude,
              shop.CreditLimit))
            .ToList());
  }

  private static TerritoryHierarchyNode
    CreateTerritoryNode(
      Territory territory,
      IReadOnlyDictionary<
        Guid,
        IReadOnlyList<ShopHierarchyNode>> shopsByTerritory)
  {
    shopsByTerritory.TryGetValue(
      territory.TerritoryId,
      out var shops);

    return new TerritoryHierarchyNode(
      territory.TerritoryId,
      territory.Code,
      territory.Name,
      shops ?? Array.Empty<ShopHierarchyNode>());
  }

  private static Dictionary<
    Guid,
    IReadOnlyList<TerritoryHierarchyNode>>
    BuildAgencyTerritoryLookup(
      IEnumerable<TerritoryAgencyAssignment> assignments,
      IReadOnlyDictionary<
        Guid,
        TerritoryHierarchyNode> territoryNodes)
  {
    return assignments
      .Where(assignment =>
        territoryNodes.ContainsKey(
          assignment.TerritoryId))
      .GroupBy(assignment => assignment.AgencyId)
      .ToDictionary(
        group => group.Key,
        group =>
          (IReadOnlyList<TerritoryHierarchyNode>)group
            .Select(assignment =>
              territoryNodes[assignment.TerritoryId])
            .OrderBy(territory => territory.Name)
            .ToList());
  }

  private static Dictionary<
    Guid,
    IReadOnlyList<AgencyHierarchyNode>>
    BuildProvinceAgencyLookup(
      IEnumerable<Agency> agencies,
      IReadOnlyDictionary<
        Guid,
        IReadOnlyList<TerritoryHierarchyNode>>
          territoriesByAgency)
  {
    return agencies
      .GroupBy(agency => agency.ProvinceId)
      .ToDictionary(
        group => group.Key,
        group =>
          (IReadOnlyList<AgencyHierarchyNode>)group
            .OrderBy(agency => agency.Name)
            .Select(agency =>
            {
              territoriesByAgency.TryGetValue(
                agency.AgencyId,
                out var territories);

              return new AgencyHierarchyNode(
                agency.AgencyId,
                agency.Name,
                territories ??
                  Array.Empty<TerritoryHierarchyNode>());
            })
            .ToList());
  }

  private static Dictionary<
    Guid,
    IReadOnlyList<TerritoryHierarchyNode>>
    BuildUnassignedTerritoryLookup(
      IEnumerable<Territory> territories,
      IReadOnlyDictionary<
        Guid,
        TerritoryHierarchyNode> territoryNodes,
      IReadOnlyDictionary<Guid, Guid> agencyIdByTerritory)
  {
    return territories
      .Where(territory =>
        !agencyIdByTerritory.ContainsKey(
          territory.TerritoryId))
      .GroupBy(territory => territory.ProvinceId)
      .ToDictionary(
        group => group.Key,
        group =>
          (IReadOnlyList<TerritoryHierarchyNode>)group
            .OrderBy(territory => territory.Name)
            .Select(territory =>
              territoryNodes[territory.TerritoryId])
            .ToList());
  }

  private static ProvinceHierarchyNode CreateProvinceNode(
    Province province,
    IReadOnlyDictionary<
      Guid,
      IReadOnlyList<AgencyHierarchyNode>>
        agenciesByProvince,
    IReadOnlyDictionary<
      Guid,
      IReadOnlyList<TerritoryHierarchyNode>>
        unassignedByProvince)
  {
    agenciesByProvince.TryGetValue(
      province.ProvinceId,
      out var agencies);

    unassignedByProvince.TryGetValue(
      province.ProvinceId,
      out var unassignedTerritories);

    return new ProvinceHierarchyNode(
      province.ProvinceId,
      province.Code,
      province.Name,
      agencies ?? Array.Empty<AgencyHierarchyNode>(),
      unassignedTerritories ??
        Array.Empty<TerritoryHierarchyNode>());
  }
}