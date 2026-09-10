namespace Sellora.CoreService.Infrastructure.Hierarchy;

/// <summary>
/// Identifiers the authenticated user is permitted to see.
/// A null collection means unrestricted within the current tenant.
/// An empty collection means the user has no visible records.
/// </summary>
internal sealed record HierarchyVisibilityScope(
  HashSet<Guid>? ProvinceIds,
  HashSet<Guid>? AgencyIds,
  HashSet<Guid>? TerritoryIds,
  HashSet<Guid>? ShopIds)
{
  public static HierarchyVisibilityScope All { get; } =
    new(null, null, null, null);

  public static HierarchyVisibilityScope None =>
    new(
      new HashSet<Guid>(),
      new HashSet<Guid>(),
      new HashSet<Guid>(),
      new HashSet<Guid>());
}