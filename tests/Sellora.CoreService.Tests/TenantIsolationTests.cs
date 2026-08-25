using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sellora.CoreService.Domain.Entities;
using Xunit;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Verifies the data-layer tenant filter: a user from one company can never
/// retrieve another company's records, regardless of request parameters.
/// </summary>
public class TenantIsolationTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public TenantIsolationTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _factory.CreateClient();
    }

    [Fact]
    public async Task CompanyAUser_SeesOnlyCompanyARecords()
    {
        var token = TestTokenFactory.CreateToken(
            _factory.Issuer, _factory.Audience, role: "SalesRep", companyId: "COMP-001");

        var records = await GetRecords("/demo-records", token);

        Assert.NotEmpty(records);
        Assert.All(records, r => Assert.Equal("COMP-001", r.CompanyId));
        Assert.DoesNotContain(records, r => r.CompanyId == "COMP-002");
    }

    [Fact]
    public async Task CompanyBUser_SeesOnlyCompanyBRecords()
    {
        var token = TestTokenFactory.CreateToken(
            _factory.Issuer, _factory.Audience, role: "SalesRep", companyId: "COMP-002");

        var records = await GetRecords("/demo-records", token);

        Assert.NotEmpty(records);
        Assert.All(records, r => Assert.Equal("COMP-002", r.CompanyId));
    }

    [Fact]
    public async Task CompanyAUser_CannotSeeCompanyB_EvenWithSpoofedParameter()
    {
        var token = TestTokenFactory.CreateToken(
            _factory.Issuer, _factory.Audience, role: "SalesRep", companyId: "COMP-001");

        // Attempt to trick the service by asking for COMP-002 explicitly.
        var records = await GetRecords("/demo-records?companyId=COMP-002", token);

        // The filter reads the token, not the query string — still only COMP-001.
        Assert.All(records, r => Assert.Equal("COMP-001", r.CompanyId));
    }

    private async Task<List<DemoRecord>> GetRecords(string path, string token)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<DemoRecord>>())!;
    }
}