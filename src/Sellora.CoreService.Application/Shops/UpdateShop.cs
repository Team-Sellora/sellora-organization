using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Application.Shops;

public sealed record UpdateShopRequest(
  Guid ShopId,
  decimal Latitude,
  decimal Longitude,
  decimal CreditLimit);

public enum UpdateShopOutcome
{
  Success,
  CallerNotAnActiveAgencyOperator,
  ShopNotFound,
  ShopOutsideCallerAgency
}

public sealed class UpdateShopResult
{
  public UpdateShopOutcome Outcome { get; }
  public string Message { get; }
  public Shop? Shop { get; }

  private UpdateShopResult(
    UpdateShopOutcome outcome,
    string message,
    Shop? shop = null)
  {
    Outcome = outcome;
    Message = message;
    Shop = shop;
  }

  public static UpdateShopResult Success(Shop shop) =>
    new(UpdateShopOutcome.Success, "Shop updated.", shop);

  public static UpdateShopResult CallerNotAnActiveAgencyOperator() =>
    new(
      UpdateShopOutcome.CallerNotAnActiveAgencyOperator,
      "Your identity is not an active Agency Operator for this company.");

  public static UpdateShopResult ShopNotFound(Guid shopId) =>
    new(
      UpdateShopOutcome.ShopNotFound,
      $"Shop '{shopId}' was not found in your company.");

  public static UpdateShopResult ShopOutsideCallerAgency(Guid shopId) =>
    new(
      UpdateShopOutcome.ShopOutsideCallerAgency,
      $"Shop '{shopId}' is in a territory not currently assigned to your agency.");
}

public interface IShopUpdateService
{
  Task<UpdateShopResult> UpdateAsync(
    UpdateShopRequest request,
    CancellationToken cancellationToken = default);
}