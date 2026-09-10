namespace Sellora.CoreService.Api.Contracts;

public sealed class UpdateShopRequestBody
{
  public decimal? Latitude { get; set; }
  public decimal? Longitude { get; set; }
  public decimal? CreditLimit { get; set; }
  public string? OwnerIdentitySub { get; set; }
}