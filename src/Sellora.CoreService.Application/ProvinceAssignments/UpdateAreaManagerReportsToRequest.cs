namespace Sellora.CoreService.Application.ProvinceAssignments;

public sealed record UpdateAreaManagerReportsToRequest(
  Guid ProvinceId,
  Guid ReportsToAdminId);
