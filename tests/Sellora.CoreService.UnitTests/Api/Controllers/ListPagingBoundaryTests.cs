using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Api.Controllers;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Application.TerritoryAssignments;
using AgencyPage = Sellora.CoreService.Application.Common.PagedResponse<Sellora.CoreService.Application.Agencies.AgencyResponse>;
using ShopPage = Sellora.CoreService.Application.Shops.PagedResponse<Sellora.CoreService.Application.Shops.ShopResponse>;
using TerritoryPage = Sellora.CoreService.Application.Common.PagedResponse<Sellora.CoreService.Application.Territories.TerritoryResponse>;

namespace Sellora.CoreService.UnitTests.Api.Controllers;

/// <summary>
/// The List actions of AgenciesController, TerritoriesController and
/// ShopsController: a pageSize on either edge of the valid range is
/// accepted and forwarded to the read service unchanged. Each Theory tests a
/// different controller, so the system under test is a local named sut
/// rather than a shared _sut field.
/// </summary>
/// <remarks>
/// <para>
/// Characteristic and blocks: pageSize relative to the valid range 1 to 100.
/// Below the range / inside the range / above the range.
/// </para>
/// <para>
/// Boundary values: the guard is pageSize &lt; 1 || pageSize &gt; 100
/// (AgenciesController and TerritoriesController through
/// PagingLimits.MaxPageSize, ShopsController with the literal 100). The
/// off-points 0 and 101 are already rows of the *_List_RejectsInvalidQuery
/// Theories in ControllerOutcomeMappingTests. These tests add the on-points
/// 1 and 100, so each edge of each endpoint is checked on both sides. The
/// data uses the literal 100, not the constant, so an accidental change to
/// PagingLimits.MaxPageSize is caught.
/// </para>
/// <para>
/// Coverage criterion: Boundary Value Analysis, inside points only, 2 rows
/// per endpoint, one Theory per endpoint.
/// </para>
/// <para>
/// Test doubles: each read service is a Moq mock. A 200 alone would not
/// catch a controller that silently clamped the value, so Verify checks the
/// query reached the service exactly once with PageSize equal to the input
/// and Page equal to the default 1. The other constructor dependencies are
/// Strict dummies: List never uses them, and a Strict dummy fails loudly if
/// it ever does.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ListPagingBoundaryTests
{
  [Theory]
  [InlineData(1)] // Lower edge, inside the range
  [InlineData(100)] // Upper edge, inside the range
  public async Task Agencies_List_PageSizeAtValidBoundary_ForwardsItToService(int pageSize)
  {
    // Arrange
    var readService = new Mock<IAgencyReadService>();
    readService
      .Setup(service => service.ListAsync(It.IsAny<AgencyListQuery>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(AgencyPage.Empty(1, pageSize));
    var sut = new AgenciesController(
      new Mock<IHierarchyDeactivationService>(MockBehavior.Strict).Object,
      new Mock<IAgencyRegistrationService>(MockBehavior.Strict).Object,
      readService.Object);

    // Act
    var result = await sut.List(
      new AgencyListQueryParams { PageSize = pageSize },
      CancellationToken.None);

    // Assert
    result.Should().BeOfType<OkObjectResult>();
    readService.Verify(
      service => service.ListAsync(
        It.Is<AgencyListQuery>(query => query.PageSize == pageSize && query.Page == 1),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Theory]
  [InlineData(1)] // Lower edge, inside the range
  [InlineData(100)] // Upper edge, inside the range
  public async Task Territories_List_PageSizeAtValidBoundary_ForwardsItToService(int pageSize)
  {
    // Arrange
    var readService = new Mock<ITerritoryReadService>();
    readService
      .Setup(service => service.ListAsync(It.IsAny<TerritoryListQuery>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(TerritoryPage.Empty(1, pageSize));
    var sut = new TerritoriesController(
      new Mock<ITerritoryRegistrationService>(MockBehavior.Strict).Object,
      readService.Object,
      new Mock<ITerritoryAgencyAssignmentService>(MockBehavior.Strict).Object,
      new Mock<ISalesRepTerritoryAssignmentService>(MockBehavior.Strict).Object);

    // Act
    var result = await sut.List(
      new TerritoryListQueryParams { PageSize = pageSize },
      CancellationToken.None);

    // Assert
    result.Should().BeOfType<OkObjectResult>();
    readService.Verify(
      service => service.ListAsync(
        It.Is<TerritoryListQuery>(query => query.PageSize == pageSize && query.Page == 1),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Theory]
  [InlineData(1)] // Lower edge, inside the range
  [InlineData(100)] // Upper edge, inside the range
  public async Task Shops_List_PageSizeAtValidBoundary_ForwardsItToService(int pageSize)
  {
    // Arrange
    var readService = new Mock<IShopReadService>();
    readService
      .Setup(service => service.ListAsync(It.IsAny<ShopListQuery>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ShopPage(Array.Empty<ShopResponse>(), 0, 1, pageSize));
    var sut = new ShopsController(
      new Mock<IShopRegistrationService>(MockBehavior.Strict).Object,
      new Mock<IShopUpdateService>(MockBehavior.Strict).Object,
      readService.Object);

    // Act
    var result = await sut.List(
      new ShopListQueryParams { PageSize = pageSize },
      CancellationToken.None);

    // Assert
    result.Should().BeOfType<OkObjectResult>();
    readService.Verify(
      service => service.ListAsync(
        It.Is<ShopListQuery>(query => query.PageSize == pageSize && query.Page == 1),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }
}
