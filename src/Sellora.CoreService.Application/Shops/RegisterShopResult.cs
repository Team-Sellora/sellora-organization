using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Application.Shops;

public enum RegisterShopOutcome
{
  Success,
  CallerNotAnActiveAgencyOperator,
  TerritoryNotFound,
  TerritoryNotAssignedToCallerAgency
}

public sealed class RegisterShopResult
{
  public RegisterShopOutcome Outcome { get; }
  public string Message { get; }
  public Shop? Shop { get; }

  private RegisterShopResult(
    RegisterShopOutcome outcome,
    string message,
    Shop? shop = null)
  {
    Outcome = outcome;
    Message = message;
    Shop = shop;
  }

  public static RegisterShopResult Success(Shop shop) =>
    new(RegisterShopOutcome.Success, "Shop registered.", shop);

  public static RegisterShopResult CallerNotAnActiveAgencyOperator() =>
    new(
      RegisterShopOutcome.CallerNotAnActiveAgencyOperator,
      "Your identity is not an active Agency Operator for this company.");

  public static RegisterShopResult TerritoryNotFound(Guid territoryId) =>
    new(
      RegisterShopOutcome.TerritoryNotFound,
      $"Territory '{territoryId}' was not found in your company.");

  public static RegisterShopResult TerritoryNotAssignedToCallerAgency(
    Guid territoryId) =>
    new(
      RegisterShopOutcome.TerritoryNotAssignedToCallerAgency,
      $"Territory '{territoryId}' is not currently assigned to your agency.");
}