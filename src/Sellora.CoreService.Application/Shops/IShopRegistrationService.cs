namespace Sellora.CoreService.Application.Shops;

public interface IShopRegistrationService
{
  Task<RegisterShopResult> RegisterAsync(
    RegisterShopRequest request,
    CancellationToken cancellationToken = default);
}