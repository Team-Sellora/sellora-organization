namespace Sellora.CoreService.Domain.Tenancy;

/// <summary>
/// Provides the current request's company (tenant) identifier, read from the
/// authenticated user's token. Consumed by the data layer to scope every query.
/// </summary>
public interface ITenantContext
{
  /// <summary>The company ID for the current request, or null if unauthenticated.</summary>
  Guid? CompanyId { get; }
}
