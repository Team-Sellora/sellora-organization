namespace Sellora.CoreService.Application.CompanyAdmins;

public sealed record CompanyAdminSummary(
  Guid StaffProfileId,
  string DisplayName,
  string? Email,
  string Status);
