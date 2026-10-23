using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Application.IdentityProvisioning;

namespace Sellora.CoreService.Tests;

/// <summary>The real API with the identity provider replaced by a fake.</summary>
public sealed class ProvisioningWebAppFactory : TestWebAppFactory
{
  public FakeIdentityProvisioner Provisioner { get; } = new();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    base.ConfigureWebHost(builder);

    builder.ConfigureTestServices(services =>
    {
      services.AddSingleton<IIdentityProvisioner>(Provisioner);
    });
  }
}

public sealed class FakeIdentityProvisioner : IIdentityProvisioner
{
  public List<NewIdentityUser> Created { get; } = new();

  public List<string> Deleted { get; } = new();

  /// <summary>Set to make the next calls fail.</summary>
  public Exception? Failure { get; set; }

  public Task<ProvisionedIdentity> CreateUserAsync(NewIdentityUser user, CancellationToken cancellationToken)
  {
    if (Failure is not null)
    {
      throw Failure;
    }

    Created.Add(user);
    return Task.FromResult(new ProvisionedIdentity(
      $"is-{Guid.NewGuid():N}",
      user.Email.ToLowerInvariant(),
      "Temp#Passw0rd1234"));
  }

  public Task DeleteUserAsync(string identityId, CancellationToken cancellationToken)
  {
    Deleted.Add(identityId);
    return Task.CompletedTask;
  }
}
