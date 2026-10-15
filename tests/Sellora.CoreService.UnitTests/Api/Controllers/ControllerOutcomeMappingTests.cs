using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Api.Controllers;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Common;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Application.TerritoryAssignments;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.UnitTests.Api.Controllers;

/// <summary>
/// Calls the controllers directly with stubbed services, so every service
/// outcome and every hand-rolled validation branch is checked against the
/// HTTP status it must map to — without needing data that provokes each one.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ControllerOutcomeMappingTests
{
  private static readonly Guid ProvinceId = Guid.NewGuid();
  private static readonly Guid TerritoryId = Guid.NewGuid();
  private static readonly Guid AgencyId = Guid.NewGuid();

  // ---------------------------------------------------------------- Agencies

  public static TheoryData<RegisterAgencyResult, int> AgencyOutcomes() => new()
  {
    { RegisterAgencyResult.CallerNotAnActiveAreaManager(), StatusCodes.Status403Forbidden },
    { RegisterAgencyResult.ProvinceNotManagedByCaller(ProvinceId), StatusCodes.Status403Forbidden },
    { RegisterAgencyResult.ProvinceNotFound(ProvinceId), StatusCodes.Status404NotFound },
    { RegisterAgencyResult.OperatorNotFound(Guid.NewGuid()), StatusCodes.Status404NotFound },
    { RegisterAgencyResult.OperatorNotAnActiveAgencyOperator(Guid.NewGuid()), StatusCodes.Status400BadRequest },
    { RegisterAgencyResult.DuplicateAgencyName("North", ProvinceId), StatusCodes.Status409Conflict },
    {
      RegisterAgencyResult.DuplicateAgencyName(
        "North",
        ProvinceId,
        new RegisterAgencyResult.DuplicateAgencyReference(AgencyId, HierarchyStatus.Inactive)),
      StatusCodes.Status409Conflict
    },
    { RegisterAgencyResult.InvalidRequest("Agency name is required."), StatusCodes.Status400BadRequest },
  };

  [Theory]
  [MemberData(nameof(AgencyOutcomes))]
  public async Task Agencies_Register_MapsEachOutcomeToItsStatus(
    RegisterAgencyResult result,
    int expectedStatus)
  {
    var controller = AgenciesControllerReturning(result);

    var response = await controller.Register(NewAgencyBody(), CancellationToken.None);

    StatusOf(response).Should().Be(expectedStatus);
    ProblemOf(response).Detail.Should().Be(result.Message);
  }

  [Fact]
  public async Task Agencies_Register_Success_Returns201WithLocationAndBody()
  {
    var agency = new Agency
    {
      AgencyId = AgencyId,
      ProvinceId = ProvinceId,
      Name = "North Agency",
      Email = "north@agency.test",
      Phone = "0112",
      Address = "1 Road",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };

    var controller = AgenciesControllerReturning(RegisterAgencyResult.Success(agency));

    var response = await controller.Register(NewAgencyBody(), CancellationToken.None);

    var created = response.Should().BeOfType<CreatedResult>().Subject;
    created.Location.Should().Be($"/api/agencies/{AgencyId}");
    var body = created.Value.Should().BeOfType<AgencyResponse>().Subject;
    body.AgencyId.Should().Be(AgencyId);
    body.Name.Should().Be("North Agency");
    body.Email.Should().Be("north@agency.test");
  }

  [Fact]
  public async Task Agencies_Register_NullBody_Returns400()
  {
    var controller = AgenciesControllerReturning(RegisterAgencyResult.InvalidRequest("unused"));

    var response = await controller.Register(null!, CancellationToken.None);

    StatusOf(response).Should().Be(StatusCodes.Status400BadRequest);
    ProblemOf(response).Title.Should().Be("Invalid request body");
  }

  [Theory]
  [InlineData(true, StatusCodes.Status204NoContent)]
  [InlineData(false, StatusCodes.Status404NotFound)]
  public async Task Agencies_Deactivate_MapsServiceResult(bool deactivated, int expectedStatus)
  {
    var controller = new AgenciesController(
      new StubDeactivationService(deactivated),
      new StubAgencyRegistrationService(RegisterAgencyResult.InvalidRequest("unused")),
      new StubAgencyReadService());

    var response = await controller.Deactivate(AgencyId, CancellationToken.None);

    StatusOf(response).Should().Be(expectedStatus);
  }

  [Theory]
  [InlineData("Unknown", null, null, "Invalid status filter")]
  [InlineData(null, 0, null, "Invalid page")]
  [InlineData(null, null, 0, "Invalid pageSize")]
  [InlineData(null, null, PagingLimits.MaxPageSize + 1, "Invalid pageSize")]
  public async Task Agencies_List_RejectsInvalidQuery(
    string? status,
    int? page,
    int? pageSize,
    string expectedTitle)
  {
    var readService = new StubAgencyReadService();
    var controller = new AgenciesController(
      new StubDeactivationService(true),
      new StubAgencyRegistrationService(RegisterAgencyResult.InvalidRequest("unused")),
      readService);

    var response = await controller.List(
      new AgencyListQueryParams { Status = status, Page = page, PageSize = pageSize },
      CancellationToken.None);

    StatusOf(response).Should().Be(StatusCodes.Status400BadRequest);
    ProblemOf(response).Title.Should().Be(expectedTitle);
    readService.LastQuery.Should().BeNull("an invalid query must never reach the service");
  }

  [Theory]
  [InlineData(null, HierarchyStatus.Active)]
  [InlineData("  Inactive ", HierarchyStatus.Inactive)]
  public async Task Agencies_List_PassesNormalisedQueryToService(string? status, string expectedStatus)
  {
    var readService = new StubAgencyReadService();
    var controller = new AgenciesController(
      new StubDeactivationService(true),
      new StubAgencyRegistrationService(RegisterAgencyResult.InvalidRequest("unused")),
      readService);

    var response = await controller.List(
      new AgencyListQueryParams { Status = status, ProvinceId = ProvinceId },
      CancellationToken.None);

    response.Should().BeOfType<OkObjectResult>();
    readService.LastQuery.Should().Be(new AgencyListQuery(
      expectedStatus,
      ProvinceId,
      PagingLimits.DefaultPage,
      PagingLimits.DefaultPageSize));
  }

  // ------------------------------------------------------------- Territories

  public static TheoryData<RegisterTerritoryResult, int> TerritoryRegistrationOutcomes() => new()
  {
    { RegisterTerritoryResult.CallerNotAnActiveAreaManager(), StatusCodes.Status403Forbidden },
    { RegisterTerritoryResult.ProvinceNotManagedByCaller(ProvinceId), StatusCodes.Status403Forbidden },
    { RegisterTerritoryResult.ProvinceNotFound(ProvinceId), StatusCodes.Status404NotFound },
    { RegisterTerritoryResult.DuplicateTerritoryCode("N-01"), StatusCodes.Status409Conflict },
    { RegisterTerritoryResult.DuplicateTerritoryName("North", ProvinceId), StatusCodes.Status409Conflict },
    { RegisterTerritoryResult.InvalidRequest("Territory code is required."), StatusCodes.Status400BadRequest },
  };

  [Theory]
  [MemberData(nameof(TerritoryRegistrationOutcomes))]
  public async Task Territories_Register_MapsEachOutcomeToItsStatus(
    RegisterTerritoryResult result,
    int expectedStatus)
  {
    var controller = TerritoriesControllerWith(registration: result);

    var response = await controller.Register(NewTerritoryBody(), CancellationToken.None);

    StatusOf(response).Should().Be(expectedStatus);
    ProblemOf(response).Detail.Should().Be(result.Message);
  }

  [Fact]
  public async Task Territories_Register_Success_Returns201WithLocationAndBody()
  {
    var territory = new Territory
    {
      TerritoryId = TerritoryId,
      ProvinceId = ProvinceId,
      Code = "N-02",
      Name = "North Two",
      GeographicDescription = "Coast",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };

    var controller = TerritoriesControllerWith(
      registration: RegisterTerritoryResult.Success(territory));

    var response = await controller.Register(NewTerritoryBody(), CancellationToken.None);

    var created = response.Should().BeOfType<CreatedResult>().Subject;
    created.Location.Should().Be($"/api/territories/{TerritoryId}");
    var body = created.Value.Should().BeOfType<TerritoryResponse>().Subject;
    body.Code.Should().Be("N-02");
    body.GeographicDescription.Should().Be("Coast");
  }

  [Fact]
  public async Task Territories_Register_NullBody_Returns400()
  {
    var controller = TerritoriesControllerWith();

    var response = await controller.Register(null!, CancellationToken.None);

    StatusOf(response).Should().Be(StatusCodes.Status400BadRequest);
  }

  public static TheoryData<AssignTerritoryAgencyResult, int> TerritoryAgencyOutcomes() => new()
  {
    { AssignTerritoryAgencyResult.CallerNotAnActiveAreaManager(), StatusCodes.Status403Forbidden },
    { AssignTerritoryAgencyResult.TerritoryNotInManagedProvinces(TerritoryId), StatusCodes.Status403Forbidden },
    { AssignTerritoryAgencyResult.AgencyNotInManagedProvinces(AgencyId), StatusCodes.Status403Forbidden },
    { AssignTerritoryAgencyResult.AgencyNotInTerritoryProvince(TerritoryId, AgencyId), StatusCodes.Status403Forbidden },
    { AssignTerritoryAgencyResult.OpenWorkBlocksReassignment(TerritoryId, new[] { "ORDER-1" }), StatusCodes.Status409Conflict },
    { AssignTerritoryAgencyResult.TerritoryNotFound(TerritoryId), StatusCodes.Status404NotFound },
    { AssignTerritoryAgencyResult.AgencyNotFound(AgencyId), StatusCodes.Status404NotFound },
  };

  [Theory]
  [MemberData(nameof(TerritoryAgencyOutcomes))]
  public async Task Territories_AssignAgency_MapsEachOutcomeToItsStatus(
    AssignTerritoryAgencyResult result,
    int expectedStatus)
  {
    var controller = TerritoriesControllerWith(agencyAssignment: result);

    var response = await controller.AssignAgency(
      TerritoryId,
      new AssignTerritoryAgencyRequestBody { AgencyId = AgencyId },
      CancellationToken.None);

    StatusOf(response).Should().Be(expectedStatus);
    ProblemOf(response).Detail.Should().Be(result.Message);
  }

  [Fact]
  public async Task Territories_AssignAgency_OpenWork_ListsBlockingReferences()
  {
    var controller = TerritoriesControllerWith(
      agencyAssignment: AssignTerritoryAgencyResult.OpenWorkBlocksReassignment(
        TerritoryId,
        new[] { "ORDER-1", "ORDER-2" }));

    var response = await controller.AssignAgency(
      TerritoryId,
      new AssignTerritoryAgencyRequestBody { AgencyId = AgencyId },
      CancellationToken.None);

    ProblemOf(response).Extensions["blockingReferences"]
      .Should().BeEquivalentTo(new[] { "ORDER-1", "ORDER-2" });
  }

  [Fact]
  public async Task Territories_AssignAgency_Success_Returns200()
  {
    var controller = TerritoriesControllerWith(
      agencyAssignment: AssignTerritoryAgencyResult.Success(new TerritoryAgencyAssignment
      {
        AssignmentId = Guid.NewGuid(),
        TerritoryId = TerritoryId,
        AgencyId = AgencyId,
        StartsAt = DateTimeOffset.UtcNow
      }));

    var response = await controller.AssignAgency(
      TerritoryId,
      new AssignTerritoryAgencyRequestBody { AgencyId = AgencyId },
      CancellationToken.None);

    response.Should().BeOfType<OkObjectResult>();
  }

  [Fact]
  public async Task Territories_AssignAgency_MissingAgencyId_Returns400()
  {
    var controller = TerritoriesControllerWith();

    var empty = await controller.AssignAgency(
      TerritoryId,
      new AssignTerritoryAgencyRequestBody(),
      CancellationToken.None);
    var missing = await controller.AssignAgency(TerritoryId, null!, CancellationToken.None);

    StatusOf(empty).Should().Be(StatusCodes.Status400BadRequest);
    StatusOf(missing).Should().Be(StatusCodes.Status400BadRequest);
  }

  public static TheoryData<AssignSalesRepToTerritoryResult, int> SalesRepOutcomes() => new()
  {
    { AssignSalesRepToTerritoryResult.CallerNotAnActiveAgencyOperator(), StatusCodes.Status403Forbidden },
    { AssignSalesRepToTerritoryResult.TerritoryNotAssignedToCallerAgency(TerritoryId), StatusCodes.Status403Forbidden },
    { AssignSalesRepToTerritoryResult.TerritoryNotFound(TerritoryId), StatusCodes.Status404NotFound },
    { AssignSalesRepToTerritoryResult.SalesRepNotFound(Guid.NewGuid()), StatusCodes.Status404NotFound },
    { AssignSalesRepToTerritoryResult.TerritoryAlreadyHasActiveSalesRep("North", "Rep", Guid.NewGuid()), StatusCodes.Status409Conflict },
    { AssignSalesRepToTerritoryResult.SalesRepAlreadyAssignedToTerritory("Rep", "N-01", "North"), StatusCodes.Status409Conflict },
    { AssignSalesRepToTerritoryResult.ConcurrentAssignmentConflict(), StatusCodes.Status409Conflict },
  };

  [Theory]
  [MemberData(nameof(SalesRepOutcomes))]
  public async Task Territories_AssignSalesRep_MapsEachOutcomeToItsStatus(
    AssignSalesRepToTerritoryResult result,
    int expectedStatus)
  {
    var controller = TerritoriesControllerWith(salesRepAssignment: result);

    var response = await controller.AssignSalesRep(
      TerritoryId,
      new AssignSalesRepToTerritoryRequestBody { SalesRepId = Guid.NewGuid() },
      CancellationToken.None);

    StatusOf(response).Should().Be(expectedStatus);
    ProblemOf(response).Detail.Should().Be(result.Message);
  }

  [Fact]
  public async Task Territories_AssignSalesRep_Success_Returns200()
  {
    var controller = TerritoriesControllerWith(
      salesRepAssignment: AssignSalesRepToTerritoryResult.Success(new SalesRepTerritoryAssignment
      {
        AssignmentId = Guid.NewGuid(),
        TerritoryId = TerritoryId,
        SalesRepId = Guid.NewGuid(),
        StartsAt = DateTimeOffset.UtcNow
      }));

    var response = await controller.AssignSalesRep(
      TerritoryId,
      new AssignSalesRepToTerritoryRequestBody { SalesRepId = Guid.NewGuid() },
      CancellationToken.None);

    response.Should().BeOfType<OkObjectResult>();
  }

  [Fact]
  public async Task Territories_AssignSalesRep_MissingSalesRepId_Returns400()
  {
    var controller = TerritoriesControllerWith();

    var empty = await controller.AssignSalesRep(
      TerritoryId,
      new AssignSalesRepToTerritoryRequestBody(),
      CancellationToken.None);
    var missing = await controller.AssignSalesRep(TerritoryId, null!, CancellationToken.None);

    StatusOf(empty).Should().Be(StatusCodes.Status400BadRequest);
    StatusOf(missing).Should().Be(StatusCodes.Status400BadRequest);
  }

  [Theory]
  [InlineData("Archived", null, null, "Invalid status filter")]
  [InlineData(null, -1, null, "Invalid page")]
  [InlineData(null, null, 0, "Invalid pageSize")]
  [InlineData(null, null, PagingLimits.MaxPageSize + 1, "Invalid pageSize")]
  public async Task Territories_List_RejectsInvalidQuery(
    string? status,
    int? page,
    int? pageSize,
    string expectedTitle)
  {
    var readService = new StubTerritoryReadService();
    var controller = TerritoriesControllerWith(readService: readService);

    var response = await controller.List(
      new TerritoryListQueryParams { Status = status, Page = page, PageSize = pageSize },
      CancellationToken.None);

    StatusOf(response).Should().Be(StatusCodes.Status400BadRequest);
    ProblemOf(response).Title.Should().Be(expectedTitle);
    readService.LastQuery.Should().BeNull();
  }

  [Fact]
  public async Task Territories_List_PassesNormalisedQueryToService()
  {
    var readService = new StubTerritoryReadService();
    var controller = TerritoriesControllerWith(readService: readService);

    var response = await controller.List(
      new TerritoryListQueryParams
      {
        Status = " Inactive ",
        ProvinceId = ProvinceId,
        Page = 2,
        PageSize = 10,
        Assigned = false
      },
      CancellationToken.None);

    response.Should().BeOfType<OkObjectResult>();
    readService.LastQuery.Should().Be(
      new TerritoryListQuery(HierarchyStatus.Inactive, ProvinceId, 2, 10, false));
  }

  // ------------------------------------------------------------------- Shops

  public static TheoryData<RegisterShopResult, int> ShopRegistrationOutcomes() => new()
  {
    { RegisterShopResult.CallerNotAnActiveAgencyOperator(), StatusCodes.Status403Forbidden },
    { RegisterShopResult.TerritoryNotAssignedToCallerAgency(TerritoryId), StatusCodes.Status403Forbidden },
    { RegisterShopResult.TerritoryNotFound(TerritoryId), StatusCodes.Status404NotFound },
    { RegisterShopResult.OwnerIdentitySubRequired(), StatusCodes.Status400BadRequest },
    { RegisterShopResult.OwnerEmailAlreadyUsed("owner@shop.test"), StatusCodes.Status409Conflict },
    { RegisterShopResult.IdentityProviderUnavailable("IS is down"), StatusCodes.Status503ServiceUnavailable },
    { RegisterShopResult.OwnerIdentityAlreadyLinked("owner-sub"), StatusCodes.Status409Conflict },
  };

  [Theory]
  [MemberData(nameof(ShopRegistrationOutcomes))]
  public async Task Shops_Register_MapsEachOutcomeToItsStatus(
    RegisterShopResult result,
    int expectedStatus)
  {
    var controller = ShopsControllerWith(registration: result);

    var response = await controller.Register(ValidShopBody(), CancellationToken.None);

    StatusOf(response).Should().Be(expectedStatus);
    ProblemOf(response).Detail.Should().Be(result.Message);
  }

  [Fact]
  public async Task Shops_Register_SuccessWithOwnerLogin_MarksResponseNoStore()
  {
    var controller = ShopsControllerWith(
      registration: RegisterShopResult.Success(
        NewShop(),
        new ProvisionedShopOwner("owner-id", "owner@shop.test", "Temp#Pass1")));

    var response = await controller.Register(ValidShopBody(), CancellationToken.None);

    response.Should().BeOfType<CreatedResult>()
      .Which.Location.Should().StartWith("/api/shops/");
    controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
  }

  [Fact]
  public async Task Shops_Register_SuccessWithoutOwnerLogin_LeavesCachingAlone()
  {
    var controller = ShopsControllerWith(registration: RegisterShopResult.Success(NewShop()));

    var response = await controller.Register(ValidShopBody(), CancellationToken.None);

    response.Should().BeOfType<CreatedResult>();
    controller.Response.Headers.CacheControl.ToString().Should().BeEmpty();
  }

  public static TheoryData<RegisterShopRequestBody?, string> InvalidShopBodies() => new()
  {
    { null, "Invalid request body" },
    { ValidShopBody(body => body.TerritoryId = Guid.Empty), "Invalid territory" },
    { ValidShopBody(body => body.Name = " "), "Invalid shop name" },
    { ValidShopBody(body => body.Address = ""), "Invalid shop address" },
    { ValidShopBody(body => body.Latitude = null), "Missing shop location" },
    { ValidShopBody(body => body.Longitude = null), "Missing shop location" },
    { ValidShopBody(body => body.Latitude = 5.0m), "Invalid latitude" },
    { ValidShopBody(body => body.Latitude = 10.0m), "Invalid latitude" },
    { ValidShopBody(body => body.Longitude = 79.0m), "Invalid longitude" },
    { ValidShopBody(body => body.Longitude = 82.0m), "Invalid longitude" },
    { ValidShopBody(body => body.CreditLimit = null), "Missing credit limit" },
    { ValidShopBody(body => body.CreditLimit = 0m), "Invalid credit limit" },
  };

  [Theory]
  [MemberData(nameof(InvalidShopBodies))]
  public async Task Shops_Register_RejectsInvalidBodyBeforeCallingService(
    RegisterShopRequestBody? body,
    string expectedTitle)
  {
    var registration = new StubShopRegistrationService(RegisterShopResult.Success(NewShop()));
    var controller = ShopsControllerWith(registrationService: registration);

    var response = await controller.Register(body!, CancellationToken.None);

    StatusOf(response).Should().Be(StatusCodes.Status400BadRequest);
    ProblemOf(response).Title.Should().Be(expectedTitle);
    registration.Calls.Should().Be(0);
  }

  public static TheoryData<UpdateShopResult, int> ShopUpdateOutcomes() => new()
  {
    { UpdateShopResult.CallerNotAnActiveAgencyOperator(), StatusCodes.Status403Forbidden },
    { UpdateShopResult.ShopOutsideCallerAgency(Guid.NewGuid()), StatusCodes.Status403Forbidden },
    { UpdateShopResult.ShopNotFound(Guid.NewGuid()), StatusCodes.Status404NotFound },
  };

  [Theory]
  [MemberData(nameof(ShopUpdateOutcomes))]
  public async Task Shops_Update_MapsEachOutcomeToItsStatus(
    UpdateShopResult result,
    int expectedStatus)
  {
    var controller = ShopsControllerWith(update: result);

    var response = await controller.Update(Guid.NewGuid(), ValidUpdateBody(), CancellationToken.None);

    StatusOf(response).Should().Be(expectedStatus);
    ProblemOf(response).Detail.Should().Be(result.Message);
  }

  [Fact]
  public async Task Shops_Update_Success_Returns200()
  {
    var controller = ShopsControllerWith(update: UpdateShopResult.Success(NewShop()));

    var response = await controller.Update(Guid.NewGuid(), ValidUpdateBody(), CancellationToken.None);

    response.Should().BeOfType<OkObjectResult>();
  }

  public static TheoryData<UpdateShopRequestBody?, string> InvalidUpdateBodies() => new()
  {
    { null, "Missing shop update values" },
    { new UpdateShopRequestBody { Longitude = 80m, CreditLimit = 1m }, "Missing shop update values" },
    { new UpdateShopRequestBody { Latitude = 7m, CreditLimit = 1m }, "Missing shop update values" },
    { new UpdateShopRequestBody { Latitude = 7m, Longitude = 80m }, "Missing shop update values" },
    { new UpdateShopRequestBody { Latitude = 5m, Longitude = 80m, CreditLimit = 1m }, "Invalid shop location" },
    { new UpdateShopRequestBody { Latitude = 10m, Longitude = 80m, CreditLimit = 1m }, "Invalid shop location" },
    { new UpdateShopRequestBody { Latitude = 7m, Longitude = 79m, CreditLimit = 1m }, "Invalid shop location" },
    { new UpdateShopRequestBody { Latitude = 7m, Longitude = 82m, CreditLimit = 1m }, "Invalid shop location" },
    { new UpdateShopRequestBody { Latitude = 7m, Longitude = 80m, CreditLimit = -5m }, "Invalid credit limit" },
  };

  [Theory]
  [MemberData(nameof(InvalidUpdateBodies))]
  public async Task Shops_Update_RejectsInvalidBodyBeforeCallingService(
    UpdateShopRequestBody? body,
    string expectedTitle)
  {
    var update = new StubShopUpdateService(UpdateShopResult.Success(NewShop()));
    var controller = ShopsControllerWith(updateService: update);

    var response = await controller.Update(Guid.NewGuid(), body!, CancellationToken.None);

    StatusOf(response).Should().Be(StatusCodes.Status400BadRequest);
    ProblemOf(response).Title.Should().Be(expectedTitle);
    update.Calls.Should().Be(0);
  }

  [Theory]
  [InlineData("Closed", null, null, "Invalid status filter")]
  [InlineData(null, 0, null, "Invalid page")]
  [InlineData(null, null, 0, "Invalid pageSize")]
  [InlineData(null, null, 101, "Invalid pageSize")]
  public async Task Shops_List_RejectsInvalidQuery(
    string? status,
    int? page,
    int? pageSize,
    string expectedTitle)
  {
    var controller = ShopsControllerWith();

    var response = await controller.List(
      new ShopListQueryParams { Status = status, Page = page, PageSize = pageSize },
      CancellationToken.None);

    StatusOf(response).Should().Be(StatusCodes.Status400BadRequest);
    ProblemOf(response).Title.Should().Be(expectedTitle);
  }

  [Fact]
  public async Task Shops_List_PassesNormalisedQueryToService()
  {
    var readService = new StubShopReadService();
    var controller = ShopsControllerWith(readService: readService);

    var response = await controller.List(
      new ShopListQueryParams { Status = "Inactive", TerritoryId = TerritoryId },
      CancellationToken.None);

    response.Should().BeOfType<OkObjectResult>();
    readService.LastQuery.Should().Be(
      new ShopListQuery(HierarchyStatus.Inactive, TerritoryId, 1, 25));
  }

  // ----------------------------------------------------------------- Helpers

  private static int? StatusOf(IActionResult result) =>
    result switch
    {
      ObjectResult objectResult => objectResult.StatusCode,
      StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
      _ => null
    };

  private static ProblemDetails ProblemOf(IActionResult result) =>
    result.Should().BeAssignableTo<ObjectResult>().Subject
      .Value.Should().BeOfType<ProblemDetails>().Subject;

  private static RegisterAgencyRequestBody NewAgencyBody() => new()
  {
    ProvinceId = ProvinceId,
    OperatorId = Guid.NewGuid(),
    Name = "North Agency"
  };

  private static RegisterTerritoryRequestBody NewTerritoryBody() => new()
  {
    ProvinceId = ProvinceId,
    Code = "N-02",
    Name = "North Two"
  };

  private static RegisterShopRequestBody ValidShopBody(
    Action<RegisterShopRequestBody>? change = null)
  {
    var body = new RegisterShopRequestBody
    {
      TerritoryId = TerritoryId,
      Name = "Corner Shop",
      OwnerEmail = "owner@shop.test",
      Address = "1 Main Street",
      Latitude = 6.9m,
      Longitude = 79.9m,
      CreditLimit = 5000m
    };

    change?.Invoke(body);
    return body;
  }

  private static UpdateShopRequestBody ValidUpdateBody() => new()
  {
    Latitude = 7m,
    Longitude = 80m,
    CreditLimit = 100m
  };

  private static Shop NewShop() => new()
  {
    ShopId = Guid.NewGuid(),
    TerritoryId = TerritoryId,
    Name = "Corner Shop",
    Address = "1 Main Street",
    Latitude = 6.9m,
    Longitude = 79.9m,
    CreditLimit = 5000m,
    Status = HierarchyStatus.Active,
    CreatedAt = DateTimeOffset.UtcNow
  };

  private static AgenciesController AgenciesControllerReturning(RegisterAgencyResult result) =>
    new(
      new StubDeactivationService(true),
      new StubAgencyRegistrationService(result),
      new StubAgencyReadService());

  private static TerritoriesController TerritoriesControllerWith(
    RegisterTerritoryResult? registration = null,
    AssignTerritoryAgencyResult? agencyAssignment = null,
    AssignSalesRepToTerritoryResult? salesRepAssignment = null,
    StubTerritoryReadService? readService = null) =>
    new(
      new StubTerritoryRegistrationService(
        registration ?? RegisterTerritoryResult.InvalidRequest("unused")),
      readService ?? new StubTerritoryReadService(),
      new StubTerritoryAgencyAssignmentService(
        agencyAssignment ?? AssignTerritoryAgencyResult.CallerNotAnActiveAreaManager()),
      new StubSalesRepAssignmentService(
        salesRepAssignment ?? AssignSalesRepToTerritoryResult.CallerNotAnActiveAgencyOperator()));

  private static ShopsController ShopsControllerWith(
    RegisterShopResult? registration = null,
    UpdateShopResult? update = null,
    StubShopRegistrationService? registrationService = null,
    StubShopUpdateService? updateService = null,
    StubShopReadService? readService = null) =>
    new(
      registrationService ?? new StubShopRegistrationService(
        registration ?? RegisterShopResult.CallerNotAnActiveAgencyOperator()),
      updateService ?? new StubShopUpdateService(
        update ?? UpdateShopResult.CallerNotAnActiveAgencyOperator()),
      readService ?? new StubShopReadService())
    {
      ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

  private sealed class StubDeactivationService(bool deactivated) : IHierarchyDeactivationService
  {
    public Task<bool> DeactivateAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(deactivated);
  }

  private sealed class StubAgencyRegistrationService(RegisterAgencyResult result) : IAgencyRegistrationService
  {
    public Task<RegisterAgencyResult> RegisterAsync(
      RegisterAgencyRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(result);
  }

  private sealed class StubAgencyReadService : IAgencyReadService
  {
    public AgencyListQuery? LastQuery { get; private set; }

    public Task<Application.Common.PagedResponse<AgencyResponse>> ListAsync(
      AgencyListQuery query,
      CancellationToken cancellationToken = default)
    {
      LastQuery = query;
      return Task.FromResult(Application.Common.PagedResponse<AgencyResponse>.Empty(query.Page, query.PageSize));
    }
  }

  private sealed class StubTerritoryRegistrationService(RegisterTerritoryResult result) : ITerritoryRegistrationService
  {
    public Task<RegisterTerritoryResult> RegisterAsync(
      RegisterTerritoryRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(result);
  }

  private sealed class StubTerritoryReadService : ITerritoryReadService
  {
    public TerritoryListQuery? LastQuery { get; private set; }

    public Task<Application.Common.PagedResponse<TerritoryResponse>> ListAsync(
      TerritoryListQuery query,
      CancellationToken cancellationToken = default)
    {
      LastQuery = query;
      return Task.FromResult(Application.Common.PagedResponse<TerritoryResponse>.Empty(query.Page, query.PageSize));
    }
  }

  private sealed class StubTerritoryAgencyAssignmentService(AssignTerritoryAgencyResult result)
    : ITerritoryAgencyAssignmentService
  {
    public Task<AssignTerritoryAgencyResult> AssignAsync(
      AssignTerritoryAgencyRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(result);
  }

  private sealed class StubSalesRepAssignmentService(AssignSalesRepToTerritoryResult result)
    : ISalesRepTerritoryAssignmentService
  {
    public Task<AssignSalesRepToTerritoryResult> AssignAsync(
      AssignSalesRepToTerritoryRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(result);
  }

  private sealed class StubShopRegistrationService(RegisterShopResult result) : IShopRegistrationService
  {
    public int Calls { get; private set; }

    public Task<RegisterShopResult> RegisterAsync(
      RegisterShopRequest request,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(result);
    }
  }

  private sealed class StubShopUpdateService(UpdateShopResult result) : IShopUpdateService
  {
    public int Calls { get; private set; }

    public Task<UpdateShopResult> UpdateAsync(
      UpdateShopRequest request,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(result);
    }
  }

  private sealed class StubShopReadService : IShopReadService
  {
    public ShopListQuery? LastQuery { get; private set; }

    public Task<Application.Shops.PagedResponse<ShopResponse>> ListAsync(
      ShopListQuery query,
      CancellationToken cancellationToken = default)
    {
      LastQuery = query;
      return Task.FromResult(new Application.Shops.PagedResponse<ShopResponse>(Array.Empty<ShopResponse>(), 0, query.Page, query.PageSize));
    }
  }
}
