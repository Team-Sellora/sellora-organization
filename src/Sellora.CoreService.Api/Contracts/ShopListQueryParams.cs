namespace Sellora.CoreService.Api.Contracts;

public sealed class ShopListQueryParams
{
  public Guid? TerritoryId { get; set; }
  public string? Status { get; set; }
  public int? Page { get; set; }
  public int? PageSize { get; set; }
}