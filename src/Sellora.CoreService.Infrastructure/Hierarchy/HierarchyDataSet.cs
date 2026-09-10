using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Hierarchy;

/// <summary>
/// Flat hierarchy rows loaded from PostgreSQL before they are composed
/// into the nested API response.
/// </summary>
internal sealed record HierarchyDataSet(
  IReadOnlyList<Province> Provinces,
  IReadOnlyList<Agency> Agencies,
  IReadOnlyList<Territory> Territories,
  IReadOnlyList<TerritoryAgencyAssignment> AgencyAssignments,
  IReadOnlyList<Shop> Shops);