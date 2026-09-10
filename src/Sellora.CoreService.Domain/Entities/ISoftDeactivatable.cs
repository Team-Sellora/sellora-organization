namespace Sellora.CoreService.Domain.Entities;

/// <summary>
/// Marks a hierarchy entity that must be deactivated instead of deleted.
/// Hard deletion is intentionally prohibited because orders, payments,
/// audit records, and historical assignments retain permanent references.
/// </summary>
public interface ISoftDeactivatable
{
  string Status { get; set; }
}