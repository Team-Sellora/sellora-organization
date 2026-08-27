namespace Sellora.CoreService.Api.Contracts;

public sealed class RegisterShopRequestBody
{
  public Guid TerritoryId { get; set; }
  public string? Name { get; set; }
  public string? OwnerName { get; set; }
  public string? OwnerEmail { get; set; }
  public string? OwnerPhone { get; set; }
  public string? Address { get; set; }
  public decimal? Latitude { get; set; }
  public decimal? Longitude { get; set; }
  public decimal? CreditLimit { get; set; }
}