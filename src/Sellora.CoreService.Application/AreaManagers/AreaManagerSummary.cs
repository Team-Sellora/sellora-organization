namespace Sellora.CoreService.Application.AreaManagers;

public sealed record AreaManagerSummary(
  Guid StaffProfileId,
  string DisplayName,
  string? Email,
  string Status);