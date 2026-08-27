namespace Sellora.CoreService.Application.Shops;

public sealed record ShopListQuery(
  string Status,
  Guid? TerritoryId,
  int Page,
  int PageSize);

public sealed record ShopResponse(
  Guid ShopId,
  Guid TerritoryId,
  string Name,
  string? OwnerName,
  string? OwnerEmail,
  string? OwnerPhone,
  string Address,
  decimal Latitude,
  decimal Longitude,
  decimal CreditLimit,
  string Status,
  DateTimeOffset CreatedAt,
  DateTimeOffset? UpdatedAt);

public sealed record PagedResponse<T>(
  IReadOnlyList<T> Items,
  int TotalCount,
  int Page,
  int PageSize);

public interface IShopReadService
{
  Task<PagedResponse<ShopResponse>> ListAsync(
    ShopListQuery query,
    CancellationToken cancellationToken = default);
}