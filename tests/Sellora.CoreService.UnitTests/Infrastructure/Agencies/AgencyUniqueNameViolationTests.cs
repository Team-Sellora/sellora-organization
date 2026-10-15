using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sellora.CoreService.Infrastructure.Agencies;

namespace Sellora.CoreService.UnitTests.Infrastructure.Agencies;

/// <summary>
/// AgencyRegistrationService.IsUniqueAgencyNameViolation (static, so there
/// is no _sut instance): decides whether a failed SaveChanges was the
/// uq_agency_province_name unique violation, which the service turns into
/// the enriched DuplicateAgencyName outcome.
/// </summary>
/// <remarks>
/// <para>
/// Characteristics and blocks (disjoint and complete per characteristic):
/// C1 inner exception: PostgresException (base) / any other exception type /
/// no inner exception.
/// C2 SqlState: 23505 unique violation (base) / any other code, represented
/// by 23503 foreign key violation.
/// C3 constraint name: uq_agency_province_name (base) / the same name in
/// different case / any other name, represented by uq_territory_company_code.
/// </para>
/// <para>
/// C2 and C3 are only defined when C1 is PostgresException: they are
/// properties of PostgresException, and the "is PostgresException pg" check
/// short-circuits before pg is read. So the 12 non-Postgres cells of the
/// 3 x 2 x 3 grid collapse into 2 distinct tests (rows 2 and 3), which pass
/// null for SqlState and constraint name.
/// </para>
/// <para>
/// Boundary values: none numeric. The case boundary of C3 is fixed by
/// StringComparison.Ordinal, which is case-sensitive (row 6).
/// </para>
/// <para>
/// Coverage criterion: Base Choice, 1 + (3-1) + (2-1) + (3-1) = 6 rows.
/// Each non-base row changes exactly one characteristic, so each row makes
/// exactly one conjunct of the &amp;&amp; chain false and shows that
/// conjunct is needed. All Combinations would add only cells that repeat a
/// conjunct already shown false.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class AgencyUniqueNameViolationTests
{
  public enum InnerExceptionKind
  {
    Postgres,
    OtherException,
    None
  }

  [Theory]
  [InlineData(InnerExceptionKind.Postgres, "23505", "uq_agency_province_name", true)] // Base: all three conjuncts true
  [InlineData(InnerExceptionKind.OtherException, null, null, false)] // C1 other exception type
  [InlineData(InnerExceptionKind.None, null, null, false)] // C1 no inner exception
  [InlineData(InnerExceptionKind.Postgres, "23503", "uq_agency_province_name", false)] // C2 other SqlState
  [InlineData(InnerExceptionKind.Postgres, "23505", "uq_territory_company_code", false)] // C3 other constraint name
  [InlineData(InnerExceptionKind.Postgres, "23505", "UQ_AGENCY_PROVINCE_NAME", false)] // C3 same name, different case (Ordinal)
  public void IsUniqueAgencyNameViolation_ExceptionShape_ReturnsTrueOnlyForAgencyNameUniqueViolation(
    InnerExceptionKind innerKind,
    string? sqlState,
    string? constraintName,
    bool expected)
  {
    // Arrange
    var exception = CreateDbUpdateException(innerKind, sqlState, constraintName);

    // Act
    var result = AgencyRegistrationService.IsUniqueAgencyNameViolation(exception);

    // Assert
    result.Should().Be(expected);
  }

  private static DbUpdateException CreateDbUpdateException(
    InnerExceptionKind innerKind,
    string? sqlState,
    string? constraintName)
  {
    return innerKind switch
    {
      InnerExceptionKind.Postgres => new DbUpdateException(
        "Saving the agency failed.",
        new PostgresException(
          "duplicate key value violates unique constraint",
          "ERROR",
          "ERROR",
          sqlState!,
          constraintName: constraintName)),
      InnerExceptionKind.OtherException => new DbUpdateException(
        "Saving the agency failed.",
        new InvalidOperationException("Not a database error.")),
      InnerExceptionKind.None => new DbUpdateException(
        "Saving the agency failed."),
      _ => throw new ArgumentOutOfRangeException(nameof(innerKind), innerKind, null)
    };
  }
}
