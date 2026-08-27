namespace Sellora.CoreService.Application.SalesRepAssignments;

/// <summary>
/// Shared contract for Order and Replenishment to verify whether a Sales Rep
/// currently covers the territory containing a shop.
/// </summary>
public interface IRepShopRelationshipVerifier
{
  Task<VerifyRepShopRelationshipResponse> VerifyAsync(
    Guid salesRepId,
    Guid shopId,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Keep this response intentionally small because it is used on every order.
/// </summary>
public sealed record VerifyRepShopRelationshipResponse(
  bool IsValid,
  string? Reason)
{
  public static VerifyRepShopRelationshipResponse Valid() =>
    new(true, null);

  public static VerifyRepShopRelationshipResponse Invalid(string reason) =>
    new(false, reason);
}