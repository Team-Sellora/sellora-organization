namespace Sellora.CoreService.Application.Common;

/// <summary>
/// Generic paged envelope used by every list endpoint. Carries the current
/// page contents plus enough metadata for the UI to render "showing 1-25
/// of 543" and drive next/previous controls without a second round-trip.
/// </summary>
public sealed record PagedResponse<T>(
  IReadOnlyList<T> Items,
  int Page,
  int PageSize,
  int TotalCount)
{
  /// <summary>Empty page — helper for silent-scoping short-circuits.</summary>
  public static PagedResponse<T> Empty(int page, int pageSize) =>
    new(Array.Empty<T>(), page, pageSize, 0);
}