namespace Sellora.CoreService.Application.Shops;

/// <summary>
/// Input for registering a shop. Company and operator identity are always
/// resolved on the server from the authenticated JWT and tenant context.
/// </summary>
public sealed record RegisterShopRequest(
  Guid TerritoryId,
  string Name,
  string? OwnerName,
  string? OwnerIdentitySub,
  string? OwnerEmail,
  string? OwnerPhone,
  string Address,
  decimal Latitude,
  decimal Longitude,
  decimal CreditLimit);