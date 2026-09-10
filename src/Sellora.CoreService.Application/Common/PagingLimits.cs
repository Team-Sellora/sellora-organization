namespace Sellora.CoreService.Application.Common;

/// <summary>
/// Shared page/pageSize bounds. Centralised so every list endpoint agrees
/// on defaults and the same maximum — no endpoint accidentally allows a
/// 10 000-row page while its neighbours cap at 100.
/// </summary>
public static class PagingLimits
{
  public const int DefaultPage = 1;
  public const int DefaultPageSize = 25;
  public const int MaxPageSize = 100;
}