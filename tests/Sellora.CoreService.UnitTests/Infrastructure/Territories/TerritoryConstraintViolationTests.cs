using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sellora.CoreService.Infrastructure.Territories;

namespace Sellora.CoreService.UnitTests.Infrastructure.Territories;

/// <summary>
/// TerritoryRegistrationService.IsConstraintViolation (static, so there is
/// no _sut instance): decides whether a failed SaveChanges was a unique
/// violation of the constraint the caller asks about. The service asks once
/// for uq_territory_company_code and once for uq_territory_province_name to
/// route the failure to DuplicateTerritoryCode or DuplicateTerritoryName.
/// </summary>
/// <remarks>
/// <para>
/// Characteristics and blocks (disjoint and complete per characteristic):
/// C1 SqlState: 23505 unique violation (base) / any other code, represented
/// by 23503 foreign key violation.
/// C2 relationship between the thrown constraint name and the asked name:
/// same (base) / different.
/// The inner exception is always a PostgresException here. Unlike T1, the
/// name this method compares against is a parameter, so the relationship
/// between thrown and asked names is the characteristic that matters.
/// </para>
/// <para>
/// Boundary values: none numeric.
/// </para>
/// <para>
/// Coverage criterion: Base Choice, 1 + (2-1) + (2-1) = 3 rows. Each
/// non-base row changes exactly one characteristic, so it makes exactly one
/// conjunct false: row 2 the SqlState conjunct, row 3 the name comparison.
/// A second "same" row with the other territory constraint would be the
/// same block as row 1 for this method, so it is not included.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TerritoryConstraintViolationTests
{
  [Theory]
  [InlineData("23505", "uq_territory_company_code", "uq_territory_company_code", true)] // Base: unique violation, thrown == asked
  [InlineData("23503", "uq_territory_company_code", "uq_territory_company_code", false)] // C1 other SqlState
  [InlineData("23505", "uq_territory_company_code", "uq_territory_province_name", false)] // C2 thrown != asked
  public void IsConstraintViolation_SqlStateAndConstraintRelationship_ReturnsTrueOnlyForUniqueViolationOfAskedConstraint(
    string sqlState,
    string thrownConstraintName,
    string askedConstraintName,
    bool expected)
  {
    // Arrange
    var exception = CreateDbUpdateException(sqlState, thrownConstraintName);

    // Act
    var result = TerritoryRegistrationService.IsConstraintViolation(
      exception,
      askedConstraintName);

    // Assert
    result.Should().Be(expected);
  }

  private static DbUpdateException CreateDbUpdateException(
    string sqlState,
    string constraintName)
  {
    return new DbUpdateException(
      "Saving the territory failed.",
      new PostgresException(
        "duplicate key value violates unique constraint",
        "ERROR",
        "ERROR",
        sqlState,
        constraintName: constraintName));
  }
}
