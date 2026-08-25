namespace Sellora.CoreService.Application.Identity;

/// <summary>
/// Provides authenticated identity information read from the JWT.
/// Values must never come from client-controlled query parameters.
/// </summary>
public interface ICurrentUserContext
{
  string? Subject { get; }
  string? Role { get; }
}