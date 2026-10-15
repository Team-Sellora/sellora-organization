using Sellora.CoreService.Application.TerritoryAssignments;
using Sellora.CoreService.Infrastructure.TerritoryAssignments;

namespace Sellora.CoreService.UnitTests.Infrastructure.TerritoryAssignments;

/// <summary>
/// TerritoryAgencyAssignmentService.DecideProvinceScope (static, so there is
/// no _sut instance): decides whether an Area Manager's live province
/// assignments allow linking a territory to an agency, and if not, which
/// check failed first. The caller manages North and East, not South.
/// </summary>
/// <remarks>
/// <para>
/// Characteristics and blocks (disjoint and complete per characteristic):
/// C1 territory province managed: yes (base) / no.
/// C2 agency province managed: yes (base) / no.
/// C3 territory and agency province: same (base) / different.
/// </para>
/// <para>
/// Combinations that cannot occur: one province cannot be both managed and
/// unmanaged, so (yes, no, same) and (no, yes, same) do not exist. The C1
/// and C2 rows of Base Choice therefore pair the unmanaged province with a
/// managed one, which makes C3 "different" in those rows.
/// </para>
/// <para>
/// Boundary values: none numeric.
/// </para>
/// <para>
/// Coverage criterion: Base Choice, 1 + (2-1) + (2-1) + (2-1) = 4 rows, plus
/// row 5 for check order. Row 5 is the cell (no, no, same), which no Base
/// Choice row covers. With both provinces unmanaged, either of the first two
/// checks could fire; the territory check must run first. That order is
/// what TerritoryAgencyAssignmentEndpointTests
/// .Put_TerritoryOutsideCallerProvinces_ReturnsForbidden was written to show
/// (South territory with South agency, expecting the territory message), but
/// its assertions would also pass for the agency message, because both
/// messages contain "not in your provinces" and the 403 title names
/// "Territory" for every scope rejection. Row 5 is the test that pins it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TerritoryAgencyProvinceScopeTests
{
  public enum Province
  {
    North,
    East,
    South
  }

  private static readonly Guid NorthProvinceId =
    Guid.Parse("6f3c2a10-0001-4000-8000-000000000001");

  private static readonly Guid EastProvinceId =
    Guid.Parse("6f3c2a10-0002-4000-8000-000000000002");

  private static readonly Guid SouthProvinceId =
    Guid.Parse("6f3c2a10-0003-4000-8000-000000000003");

  [Theory]
  [InlineData(Province.North, Province.North, AssignTerritoryAgencyOutcome.Success)] // Base: both managed, same province
  [InlineData(Province.South, Province.North, AssignTerritoryAgencyOutcome.TerritoryNotInManagedProvinces)] // C1 territory unmanaged
  [InlineData(Province.North, Province.South, AssignTerritoryAgencyOutcome.AgencyNotInManagedProvinces)] // C2 agency unmanaged
  [InlineData(Province.North, Province.East, AssignTerritoryAgencyOutcome.AgencyNotInTerritoryProvince)] // C3 different managed provinces
  [InlineData(Province.South, Province.South, AssignTerritoryAgencyOutcome.TerritoryNotInManagedProvinces)] // Check order: both unmanaged, territory reported first
  public void DecideProvinceScope_TerritoryAndAgencyProvince_ReturnsFirstFailingCheckOrSuccess(
    Province territoryProvince,
    Province agencyProvince,
    AssignTerritoryAgencyOutcome expected)
  {
    // Arrange
    var managedProvinceIds = CreateManagedProvinceIds();
    var territoryProvinceId = ToProvinceId(territoryProvince);
    var agencyProvinceId = ToProvinceId(agencyProvince);

    // Act
    var result = TerritoryAgencyAssignmentService.DecideProvinceScope(
      managedProvinceIds,
      territoryProvinceId,
      agencyProvinceId);

    // Assert
    result.Should().Be(expected);
  }

  private static IReadOnlySet<Guid> CreateManagedProvinceIds()
  {
    return new HashSet<Guid> { NorthProvinceId, EastProvinceId };
  }

  private static Guid ToProvinceId(Province province)
  {
    return province switch
    {
      Province.North => NorthProvinceId,
      Province.East => EastProvinceId,
      Province.South => SouthProvinceId,
      _ => throw new ArgumentOutOfRangeException(nameof(province), province, null)
    };
  }
}
