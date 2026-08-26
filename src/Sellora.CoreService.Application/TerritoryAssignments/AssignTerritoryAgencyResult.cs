using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Application.TerritoryAssignments;

public enum AssignTerritoryAgencyOutcome
{
  Success,
  CallerNotAnActiveAreaManager,
  TerritoryNotFound,
  AgencyNotFound,
  TerritoryNotInManagedProvinces,
  AgencyNotInManagedProvinces,
  AgencyNotInTerritoryProvince,
  OpenWorkBlocksReassignment
}

public sealed class AssignTerritoryAgencyResult
{
  public AssignTerritoryAgencyOutcome Outcome { get; }
  public string Message { get; }
  public TerritoryAgencyAssignment? Assignment { get; }
  public IReadOnlyList<string> BlockingReferences { get; }

  private AssignTerritoryAgencyResult(
    AssignTerritoryAgencyOutcome outcome,
    string message,
    TerritoryAgencyAssignment? assignment = null,
    IReadOnlyList<string>? blockingReferences = null)
  {
    Outcome = outcome;
    Message = message;
    Assignment = assignment;
    BlockingReferences = blockingReferences ?? Array.Empty<string>();
  }

  public static AssignTerritoryAgencyResult Success(
    TerritoryAgencyAssignment assignment) =>
    new(AssignTerritoryAgencyOutcome.Success, "Territory assigned to agency.", assignment);

  public static AssignTerritoryAgencyResult CallerNotAnActiveAreaManager() =>
    new(
      AssignTerritoryAgencyOutcome.CallerNotAnActiveAreaManager,
      "The caller is not an active Area Manager in this company.");

  public static AssignTerritoryAgencyResult TerritoryNotFound(Guid territoryId) =>
    new(
      AssignTerritoryAgencyOutcome.TerritoryNotFound,
      $"Territory '{territoryId}' was not found in your company.");

  public static AssignTerritoryAgencyResult AgencyNotFound(Guid agencyId) =>
    new(
      AssignTerritoryAgencyOutcome.AgencyNotFound,
      $"Agency '{agencyId}' was not found in your company.");

  public static AssignTerritoryAgencyResult TerritoryNotInManagedProvinces(
    Guid territoryId) =>
    new(
      AssignTerritoryAgencyOutcome.TerritoryNotInManagedProvinces,
      $"Territory '{territoryId}' is not in your provinces.");

  public static AssignTerritoryAgencyResult AgencyNotInManagedProvinces(
    Guid agencyId) =>
    new(
      AssignTerritoryAgencyOutcome.AgencyNotInManagedProvinces,
      $"Agency '{agencyId}' is not in your provinces.");

  public static AssignTerritoryAgencyResult AgencyNotInTerritoryProvince(
    Guid territoryId,
    Guid agencyId) =>
    new(
      AssignTerritoryAgencyOutcome.AgencyNotInTerritoryProvince,
      $"Agency '{agencyId}' is not in the same province as territory '{territoryId}'.");

  public static AssignTerritoryAgencyResult OpenWorkBlocksReassignment(
    Guid territoryId,
    IReadOnlyList<string> blockingReferences) =>
    new(
      AssignTerritoryAgencyOutcome.OpenWorkBlocksReassignment,
      $"Territory '{territoryId}' cannot be reassigned while it has open work.",
      blockingReferences: blockingReferences);
}