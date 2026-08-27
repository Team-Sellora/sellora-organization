using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Application.SalesRepAssignments;

public enum AssignSalesRepToTerritoryOutcome
{
  Success,
  CallerNotAnActiveAgencyOperator,
  TerritoryNotFound,
  TerritoryNotAssignedToCallerAgency,
  SalesRepNotFound,
  TerritoryAlreadyHasActiveSalesRep,
  SalesRepAlreadyAssignedToTerritory,
  ConcurrentAssignmentConflict
}

public sealed class AssignSalesRepToTerritoryResult
{
  public AssignSalesRepToTerritoryOutcome Outcome { get; }
  public string Message { get; }
  public SalesRepTerritoryAssignment? Assignment { get; }

  private AssignSalesRepToTerritoryResult(
    AssignSalesRepToTerritoryOutcome outcome,
    string message,
    SalesRepTerritoryAssignment? assignment = null)
  {
    Outcome = outcome;
    Message = message;
    Assignment = assignment;
  }

  public static AssignSalesRepToTerritoryResult Success(
    SalesRepTerritoryAssignment assignment) =>
    new(AssignSalesRepToTerritoryOutcome.Success,
      "Sales Rep assigned to territory.",
      assignment);

  public static AssignSalesRepToTerritoryResult CallerNotAnActiveAgencyOperator() =>
    new(AssignSalesRepToTerritoryOutcome.CallerNotAnActiveAgencyOperator,
      "The caller is not an active Agency Operator with an active agency assignment.");

  public static AssignSalesRepToTerritoryResult TerritoryNotFound(Guid territoryId) =>
    new(AssignSalesRepToTerritoryOutcome.TerritoryNotFound,
      $"Territory '{territoryId}' was not found in your company.");

  public static AssignSalesRepToTerritoryResult TerritoryNotAssignedToCallerAgency(
    Guid territoryId) =>
    new(AssignSalesRepToTerritoryOutcome.TerritoryNotAssignedToCallerAgency,
      $"Territory '{territoryId}' is not assigned to your agency.");

  public static AssignSalesRepToTerritoryResult SalesRepNotFound(Guid salesRepId) =>
    new(AssignSalesRepToTerritoryOutcome.SalesRepNotFound,
      $"Sales Rep '{salesRepId}' was not found or is inactive.");

  public static AssignSalesRepToTerritoryResult TerritoryAlreadyHasActiveSalesRep(
    string territoryName,
    string existingRepName,
    Guid existingRepId) =>
    new(AssignSalesRepToTerritoryOutcome.TerritoryAlreadyHasActiveSalesRep,
      $"Territory '{territoryName}' already has an active rep: " +
      $"'{existingRepName}' ({existingRepId}).");

  public static AssignSalesRepToTerritoryResult SalesRepAlreadyAssignedToTerritory(
    string salesRepName,
    string existingTerritoryCode,
    string existingTerritoryName) =>
    new(AssignSalesRepToTerritoryOutcome.SalesRepAlreadyAssignedToTerritory,
      $"Sales Rep '{salesRepName}' is already assigned to territory " +
      $"'{existingTerritoryCode} — {existingTerritoryName}'.");

  public static AssignSalesRepToTerritoryResult ConcurrentAssignmentConflict() =>
    new(AssignSalesRepToTerritoryOutcome.ConcurrentAssignmentConflict,
      "The assignment changed concurrently. Reload the territory and try again.");
}