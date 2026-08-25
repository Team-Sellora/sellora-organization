using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class Shop : ITenantScoped
{
  public Guid ShopId { get; set; }
  public Guid CompanyId { get; set; }
  public Guid TerritoryId { get; set; }
  public string Name { get; set; } = string.Empty;
  public string? OwnerName { get; set; }
  public string? OwnerIdentitySub { get; set; }   // unique when present
  public string? OwnerEmail { get; set; }
  public string? OwnerPhone { get; set; }
  public string Address { get; set; } = string.Empty;
  public decimal Latitude { get; set; }            // numeric(9,6)
  public decimal Longitude { get; set; }           // numeric(9,6)
  public decimal CreditLimit { get; set; }         // numeric(18,2)
  public string Status { get; set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset? UpdatedAt { get; set; }
}