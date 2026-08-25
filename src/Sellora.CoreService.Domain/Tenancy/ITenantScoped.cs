namespace Sellora.CoreService.Domain.Tenancy;

/// <summary>
/// Marks an entity as belonging to a company (tenant). Every entity implementing
/// this is automatically subject to the tenant query filter, so no tenant-scoped
/// table can accidentally escape company isolation.
/// </summary>
public interface ITenantScoped
{
  Guid CompanyId { get; }
}