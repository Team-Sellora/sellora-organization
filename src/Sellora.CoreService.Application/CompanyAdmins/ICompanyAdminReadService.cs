namespace Sellora.CoreService.Application.CompanyAdmins;

public interface ICompanyAdminReadService
{
  Task<IReadOnlyList<CompanyAdminSummary>> ListActiveAsync(
    CancellationToken cancellationToken = default);
}
