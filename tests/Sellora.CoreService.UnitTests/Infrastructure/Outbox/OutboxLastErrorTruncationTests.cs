using Sellora.CoreService.Infrastructure.Outbox;

namespace Sellora.CoreService.UnitTests.Infrastructure.Outbox;

/// <summary>
/// OutboxRelayService.TruncateLastError (static, so there is no _sut
/// instance): shortens a publish failure message so it fits the last_error
/// column, which has a maximum length of 2000.
/// </summary>
/// <remarks>
/// <para>
/// Characteristic and blocks (disjoint and complete): input length compared
/// with 2000. Well under 2000 / exactly 2000 / 2001 / far over 2000.
/// </para>
/// <para>
/// Boundary values: the condition is Length &gt; 2000. The 2000 row pins
/// that exactly 2000 is kept unchanged and the 2001 row pins that 2001 is
/// the first length cut. Changing &gt; to &gt;= is an equivalent mutant that
/// no test can kill, because Substring(0, 2000) of a 2000 character string
/// is the same string.
/// </para>
/// <para>
/// Coverage criterion: Boundary Value Analysis on the 2000 limit plus one
/// row from each remaining block, 4 rows.
/// </para>
/// <para>
/// Each row asserts the exact length and that the result is the first
/// characters of the input. The input cycles through a to z, so keeping the
/// last 2000 characters instead of the first would start with b (2001) or
/// k (5000) and fail. A single repeated character would hide that fault.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class OutboxLastErrorTruncationTests
{
  [Theory]
  [InlineData(11, 11)] // Well under 2000: kept whole
  [InlineData(2000, 2000)] // Boundary, exactly 2000: kept whole
  [InlineData(2001, 2000)] // Boundary, 2001: first length that is cut
  [InlineData(5000, 2000)] // Far over 2000: cut
  public void TruncateLastError_InputLength_ReturnsPrefixOfAtMost2000Characters(
    int inputLength,
    int expectedLength)
  {
    // Arrange
    var errorMessage = CreateErrorMessage(inputLength);

    // Act
    var result = OutboxRelayService.TruncateLastError(errorMessage);

    // Assert
    result.Should().HaveLength(expectedLength);
    result.Should().Be(errorMessage.Substring(0, expectedLength));
  }

  private static string CreateErrorMessage(int length)
  {
    return new string(
      Enumerable.Range(0, length)
        .Select(index => (char)('a' + index % 26))
        .ToArray());
  }
}
