using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Application.Shops;

public enum RegisterShopOutcome
{
  Success,
  CallerNotAnActiveAgencyOperator,
  TerritoryNotFound,
  TerritoryNotAssignedToCallerAgency,
  OwnerIdentitySubRequired,
  OwnerIdentityAlreadyLinked,
  OwnerEmailAlreadyUsed,
  IdentityProviderUnavailable
}

public sealed class RegisterShopResult
{
  public RegisterShopOutcome Outcome { get; }
  public string Message { get; }
  public Shop? Shop { get; }

  /// <summary>
  /// The Shop Owner login created with the shop, when one was provisioned.
  /// Carries a temporary password once; never stored.
  /// </summary>
  public ProvisionedShopOwner? OwnerLogin { get; }

  private RegisterShopResult(
    RegisterShopOutcome outcome,
    string message,
    Shop? shop = null,
    ProvisionedShopOwner? ownerLogin = null)
  {
    Outcome = outcome;
    Message = message;
    Shop = shop;
    OwnerLogin = ownerLogin;
  }

  public static RegisterShopResult Success(Shop shop, ProvisionedShopOwner? ownerLogin = null) =>
    new(RegisterShopOutcome.Success, "Shop registered.", shop, ownerLogin);

  public static RegisterShopResult OwnerEmailAlreadyUsed(string email) =>
    new(
      RegisterShopOutcome.OwnerEmailAlreadyUsed,
      $"A login for {email} already exists. Use a different email for this shop owner.");

  public static RegisterShopResult IdentityProviderUnavailable(string message) =>
    new(RegisterShopOutcome.IdentityProviderUnavailable, message);

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

  public static RegisterShopResult OwnerIdentitySubRequired() =>
    new(
      RegisterShopOutcome.OwnerIdentitySubRequired,
      "ownerEmail is required so a login can be created for the Shop Owner.");

  public static RegisterShopResult OwnerIdentityAlreadyLinked(
    string ownerIdentitySub) =>
    new(
      RegisterShopOutcome.OwnerIdentityAlreadyLinked,
      $"Shop Owner identity '{ownerIdentitySub}' is already linked to another shop.");
}

public sealed record ProvisionedShopOwner(
  string IdentitySub,
  string UserName,
  string? TemporaryPassword);
