using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Api.Controllers;
using Sellora.CoreService.Application.ProvinceAssignments;
using Sellora.CoreService.Application.Provinces;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;

namespace Sellora.CoreService.UnitTests.Api.Controllers;

/// <summary>
/// ProvincesController.AssignAreaManager (_sut): validates the body, builds
/// the request from the route province and the body manager, calls the
/// assignment service and maps its outcome to an HTTP result.
/// </summary>
/// <remarks>
/// <para>
/// Outcome mapping. Characteristic and blocks (disjoint and complete): the
/// service outcome, one block per AssignAreaManagerOutcome value. Coverage
/// criterion: Each Choice, 7 rows; with a single characteristic Base Choice
/// adds nothing. Each row asserts status, title and detail. The title is
/// what separates NoActiveCompanyAdmin from the default arm: both are 400,
/// so deleting that arm would only change the title.
/// </para>
/// <para>
/// Forwarding. One Fact checks that the request sent to the service takes
/// the province from the route and the manager from the body, exactly once.
/// </para>
/// <para>
/// Invalid body. Characteristic and blocks: body shape, null / an
/// AreaManagerId of Guid.Empty (both operands of the guard's ||). Coverage
/// criterion: Each Choice, 2 rows. Both must return 400 without calling the
/// service.
/// </para>
/// <para>
/// Boundary values: none numeric. Guid.Empty is the boundary of "missing"
/// for AreaManagerId.
/// </para>
/// <para>
/// Test doubles: IProvinceAssignmentService is a Moq stub in the mapping
/// Theory (it only returns a canned result), a mock in the forwarding Fact
/// (Verify on the exact request, Times.Once) and a mock in the invalid-body
/// Theory (VerifyNoOtherCalls, used only on this rejection path).
/// IProvinceReadService is a dummy: the constructor needs it but
/// AssignAreaManager never uses it. It is Strict, so if production code
/// ever called it the test would fail with a MockException naming the call,
/// instead of a Loose dummy quietly returning a default.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ProvincesControllerTests
{
  public enum BodyKind
  {
    Null,
    EmptyAreaManagerId
  }

  private static readonly Guid ProvinceId =
    Guid.Parse("a1b2c3d4-0001-4000-8000-000000000001");

  private static readonly Guid AreaManagerId =
    Guid.Parse("a1b2c3d4-0002-4000-8000-000000000002");

  private static readonly Guid OtherProvinceId =
    Guid.Parse("a1b2c3d4-0003-4000-8000-000000000003");

  private readonly Mock<IProvinceAssignmentService> _assignments = new();
  private readonly Mock<IProvinceReadService> _provinces = new(MockBehavior.Strict);
  private readonly ProvincesController _sut;

  public ProvincesControllerTests()
  {
    _sut = new ProvincesController(_assignments.Object, _provinces.Object);
  }

  [Theory]
  [InlineData(AssignAreaManagerOutcome.Success, 200, null)] // Success: 200 with the assignment, no problem details
  [InlineData(AssignAreaManagerOutcome.ProvinceNotFound, 404, "Province not found")] // ProvinceNotFound arm
  [InlineData(AssignAreaManagerOutcome.TargetUserNotFound, 400, "Assignment rejected")] // TargetUserNotFound, default arm
  [InlineData(AssignAreaManagerOutcome.TargetNotAreaManager, 400, "Assignment rejected")] // TargetNotAreaManager, default arm
  [InlineData(AssignAreaManagerOutcome.TargetInactive, 400, "Assignment rejected")] // TargetInactive, default arm
  [InlineData(AssignAreaManagerOutcome.NoActiveCompanyAdmin, 400, "No reporting admin available")] // NoActiveCompanyAdmin arm
  [InlineData(AssignAreaManagerOutcome.TargetAlreadyManagesAnotherProvince, 409, "Target already manages another province")] // Conflict arm
  public async Task AssignAreaManager_ServiceOutcome_MapsToStatusAndTitle(
    AssignAreaManagerOutcome outcome,
    int expectedStatus,
    string? expectedTitle)
  {
    // Arrange
    var (serviceResult, expectedDetail) = CreateServiceResult(outcome);
    _assignments
      .Setup(service => service.AssignAreaManagerAsync(
        It.IsAny<AssignAreaManagerRequest>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(serviceResult);

    // Act
    var result = await _sut.AssignAreaManager(
      ProvinceId,
      new AssignAreaManagerRequestBody { AreaManagerId = AreaManagerId },
      CancellationToken.None);

    // Assert
    var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
    var problem = objectResult.Value as ProblemDetails;
    objectResult.StatusCode.Should().Be(expectedStatus);
    var title = problem?.Title;
    var detail = problem?.Detail;
    title.Should().Be(expectedTitle);
    detail.Should().Be(expectedDetail);
  }

  [Fact]
  public async Task AssignAreaManager_ValidBody_ForwardsRouteProvinceAndBodyManagerToService()
  {
    // Arrange
    var (serviceResult, _) = CreateServiceResult(AssignAreaManagerOutcome.Success);
    _assignments
      .Setup(service => service.AssignAreaManagerAsync(
        It.IsAny<AssignAreaManagerRequest>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(serviceResult);

    // Act
    await _sut.AssignAreaManager(
      ProvinceId,
      new AssignAreaManagerRequestBody { AreaManagerId = AreaManagerId },
      CancellationToken.None);

    // Assert
    _assignments.Verify(
      service => service.AssignAreaManagerAsync(
        It.Is<AssignAreaManagerRequest>(request =>
          request.ProvinceId == ProvinceId &&
          request.AreaManagerId == AreaManagerId),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Theory]
  [InlineData(BodyKind.Null)] // Body missing
  [InlineData(BodyKind.EmptyAreaManagerId)] // AreaManagerId is Guid.Empty
  public async Task AssignAreaManager_MissingAreaManagerId_Returns400WithoutCallingService(
    BodyKind bodyKind)
  {
    // Arrange
    var body = CreateBody(bodyKind);

    // Act
    var result = await _sut.AssignAreaManager(
      ProvinceId,
      body!,
      CancellationToken.None);

    // Assert
    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
    badRequest.StatusCode.Should().Be(400);
    problem.Title.Should().Be("Invalid request body");
    problem.Detail.Should().Be("areaManagerId is required.");
    _assignments.VerifyNoOtherCalls();
  }

  private static (AssignAreaManagerResult Result, string? ExpectedDetail) CreateServiceResult(
    AssignAreaManagerOutcome outcome)
  {
    var target = new StaffProfile
    {
      StaffProfileId = AreaManagerId,
      Role = Roles.AreaManager,
      Status = "Active"
    };

    var result = outcome switch
    {
      AssignAreaManagerOutcome.Success => AssignAreaManagerResult.Success(
        new ProvinceManagerAssignment
        {
          AssignmentId = Guid.Parse("a1b2c3d4-0004-4000-8000-000000000004"),
          ProvinceId = ProvinceId,
          AreaManagerId = AreaManagerId,
          StartsAt = new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero)
        }),
      AssignAreaManagerOutcome.ProvinceNotFound =>
        AssignAreaManagerResult.ProvinceNotFound(ProvinceId),
      AssignAreaManagerOutcome.TargetUserNotFound =>
        AssignAreaManagerResult.TargetUserNotFound(AreaManagerId),
      AssignAreaManagerOutcome.TargetNotAreaManager =>
        AssignAreaManagerResult.TargetNotAreaManager(target),
      AssignAreaManagerOutcome.TargetInactive =>
        AssignAreaManagerResult.TargetInactive(target),
      AssignAreaManagerOutcome.NoActiveCompanyAdmin =>
        AssignAreaManagerResult.NoActiveCompanyAdmin(),
      AssignAreaManagerOutcome.TargetAlreadyManagesAnotherProvince =>
        AssignAreaManagerResult.TargetAlreadyManagesAnotherProvince(target, OtherProvinceId),
      _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };

    // A success returns the assignment, not problem details, so it has no detail.
    var expectedDetail = result.IsSuccess ? null : result.Message;

    return (result, expectedDetail);
  }

  private static AssignAreaManagerRequestBody? CreateBody(BodyKind bodyKind)
  {
    return bodyKind switch
    {
      BodyKind.Null => null,
      BodyKind.EmptyAreaManagerId => new AssignAreaManagerRequestBody
      {
        AreaManagerId = Guid.Empty
      },
      _ => throw new ArgumentOutOfRangeException(nameof(bodyKind), bodyKind, null)
    };
  }
}
