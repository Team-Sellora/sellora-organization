namespace Sellora.CoreService.Domain.Entities;

/// <summary>
/// A minimal tenant-scoped entity used to demonstrate company data isolation.
/// Every tenant-scoped entity carries a <see cref="CompanyId"/>.
/// </summary>
public class DemoRecord
{
  public int Id { get; set; }
  public string CompanyId { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
}