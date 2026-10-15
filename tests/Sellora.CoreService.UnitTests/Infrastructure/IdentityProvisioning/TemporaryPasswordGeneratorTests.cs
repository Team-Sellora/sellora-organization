using Sellora.CoreService.Infrastructure.IdentityProvisioning;

namespace Sellora.CoreService.Tests;

public sealed class TemporaryPasswordGeneratorTests
{
  [Fact]
  public void Passwords_meet_the_default_IS_policy_and_differ()
  {
    var passwords = Enumerable.Range(0, 50).Select(_ => TemporaryPasswordGenerator.Generate()).ToList();

    Assert.All(passwords, password =>
    {
      Assert.Equal(16, password.Length);
      Assert.Contains(password, char.IsUpper);
      Assert.Contains(password, char.IsLower);
      Assert.Contains(password, char.IsDigit);
      Assert.Contains(password, character => !char.IsLetterOrDigit(character));
    });
    Assert.Equal(passwords.Count, passwords.Distinct().Count());
  }
}
