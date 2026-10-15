using System.Text.Json;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Outbox;

namespace Sellora.CoreService.UnitTests.Infrastructure.Outbox;

/// <summary>
/// HierarchyEventFactory (_sut): builds the outbox envelope for each
/// hierarchy event from the fields of its source entities, with the
/// correlation ID of the current request in the payload.
/// </summary>
/// <remarks>
/// <para>
/// Characteristic and blocks (disjoint and complete): the event kind, one
/// block per factory method, with ShopUpdated taken in its UpdatedAt-present
/// form. Coverage criterion: Each Choice, 6 rows.
/// </para>
/// <para>
/// Every entity uses its own fixed GUID and its own fixed timestamp, so an
/// envelope built from the wrong source field (for example the territory
/// instead of the assignment, or CreatedAt instead of UpdatedAt) produces a
/// different value and fails the row.
/// </para>
/// <para>
/// Boundary values: none numeric. ShopUpdated has a second block, UpdatedAt
/// null, where OccurredAt falls back to the current time. That value cannot
/// be fixed without a clock abstraction (production code reads
/// DateTimeOffset.UtcNow), so a separate Fact records the time before and
/// after the call and asserts OccurredAt lies between them.
/// </para>
/// <para>
/// Test double: ICorrelationIdAccessor is a Moq stub returning
/// "corr-stub-1". The test checks that value inside the payload, not that
/// the accessor was called.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class HierarchyEventFactoryTests
{
  public enum EventKind
  {
    AgencyRegistered,
    TerritoryAssignedToAgency,
    ShopRegistered,
    ShopUpdatedWithUpdatedAt,
    SalesRepAssigned,
    HierarchyEntityDeactivated
  }

  private const string StubCorrelationId = "corr-stub-1";

  private static readonly Guid CompanyId = Guid.Parse("c0000000-0000-4000-8000-000000000001");
  private static readonly Guid ProvinceId = Guid.Parse("c0000000-0000-4000-8000-000000000002");
  private static readonly Guid AgencyId = Guid.Parse("c0000000-0000-4000-8000-000000000003");
  private static readonly Guid TerritoryId = Guid.Parse("c0000000-0000-4000-8000-000000000004");
  private static readonly Guid TerritoryAssignmentId = Guid.Parse("c0000000-0000-4000-8000-000000000005");
  private static readonly Guid ShopId = Guid.Parse("c0000000-0000-4000-8000-000000000006");
  private static readonly Guid SalesRepId = Guid.Parse("c0000000-0000-4000-8000-000000000007");
  private static readonly Guid SalesRepAssignmentId = Guid.Parse("c0000000-0000-4000-8000-000000000008");
  private static readonly Guid OperatorId = Guid.Parse("c0000000-0000-4000-8000-000000000009");

  private static readonly DateTimeOffset AgencyCreatedAt = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset TerritoryCreatedAt = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset TerritoryAssignmentStartsAt = new(2026, 8, 26, 11, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset ShopCreatedAt = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset ShopUpdatedAt = new(2026, 8, 28, 13, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset SalesRepAssignmentStartsAt = new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset DeactivatedAt = new(2026, 8, 30, 15, 0, 0, TimeSpan.Zero);

  private readonly Mock<ICorrelationIdAccessor> _correlationIdAccessor = new();
  private readonly HierarchyEventFactory _sut;

  public HierarchyEventFactoryTests()
  {
    _correlationIdAccessor
      .Setup(accessor => accessor.GetCorrelationId())
      .Returns(StubCorrelationId);

    _sut = new HierarchyEventFactory(_correlationIdAccessor.Object);
  }

  public static TheoryData<EventKind, string, Guid, string, DateTimeOffset> EnvelopeCases => new()
  {
    { EventKind.AgencyRegistered, "Agency", AgencyId, "AgencyRegistered", AgencyCreatedAt }, // AgencyRegistered
    { EventKind.TerritoryAssignedToAgency, "Territory", TerritoryId, "TerritoryAssignedToAgency", TerritoryAssignmentStartsAt }, // TerritoryAssignedToAgency
    { EventKind.ShopRegistered, "Shop", ShopId, "ShopRegistered", ShopCreatedAt }, // ShopRegistered
    { EventKind.ShopUpdatedWithUpdatedAt, "Shop", ShopId, "ShopUpdated", ShopUpdatedAt }, // ShopUpdated, UpdatedAt present
    { EventKind.SalesRepAssigned, "SalesRep", SalesRepId, "SalesRepAssigned", SalesRepAssignmentStartsAt }, // SalesRepAssigned
    { EventKind.HierarchyEntityDeactivated, "Agency", AgencyId, "HierarchyEntityDeactivated", DeactivatedAt } // HierarchyEntityDeactivated
  };

  [Theory]
  [MemberData(nameof(EnvelopeCases))]
  public void Create_EachEventKind_BuildsEnvelopeFromItsSourceFields(
    EventKind eventKind,
    string expectedAggregateType,
    Guid expectedAggregateId,
    string expectedEventType,
    DateTimeOffset expectedOccurredAt)
  {
    // Arrange
    var createEvent = CreateEventCall(eventKind);

    // Act
    var message = createEvent(_sut);

    // Assert
    using var payload = JsonDocument.Parse(message.Payload);
    message.CompanyId.Should().Be(CompanyId);
    message.AggregateType.Should().Be(expectedAggregateType);
    message.AggregateId.Should().Be(expectedAggregateId);
    message.EventType.Should().Be(expectedEventType);
    message.SchemaVersion.Should().Be("1.0");
    message.OccurredAt.Should().Be(expectedOccurredAt);
    payload.RootElement.GetProperty("eventType").GetString().Should().Be(expectedEventType);
    payload.RootElement.GetProperty("correlationId").GetString().Should().Be(StubCorrelationId);
  }

  [Fact]
  public void ShopUpdated_WithoutUpdatedAt_UsesCurrentTimeAsOccurredAt()
  {
    // Arrange
    var shop = CreateShop(updatedAt: null);
    var before = DateTimeOffset.UtcNow;

    // Act
    var message = _sut.ShopUpdated(shop, AgencyId, new[] { "Name" });

    // Assert
    var after = DateTimeOffset.UtcNow;
    message.OccurredAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
  }

  private static Func<HierarchyEventFactory, NewOutboxMessage> CreateEventCall(EventKind eventKind)
  {
    return eventKind switch
    {
      EventKind.AgencyRegistered =>
        factory => factory.AgencyRegistered(CreateAgency(), OperatorId),
      EventKind.TerritoryAssignedToAgency =>
        factory => factory.TerritoryAssignedToAgency(CreateTerritory(), CreateTerritoryAssignment()),
      EventKind.ShopRegistered =>
        factory => factory.ShopRegistered(CreateShop(updatedAt: null), AgencyId),
      EventKind.ShopUpdatedWithUpdatedAt =>
        factory => factory.ShopUpdated(CreateShop(ShopUpdatedAt), AgencyId, new[] { "Name" }),
      EventKind.SalesRepAssigned =>
        factory => factory.SalesRepAssigned(CreateSalesRepAssignment(), AgencyId, "Nimal Perera"),
      EventKind.HierarchyEntityDeactivated =>
        factory => factory.HierarchyEntityDeactivated(CreateAgency(), DeactivatedAt),
      _ => throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, null)
    };
  }

  private static Agency CreateAgency()
  {
    return new Agency
    {
      AgencyId = AgencyId,
      CompanyId = CompanyId,
      ProvinceId = ProvinceId,
      Name = "Colombo Distribution Agency",
      Status = "Active",
      CreatedAt = AgencyCreatedAt
    };
  }

  private static Territory CreateTerritory()
  {
    return new Territory
    {
      TerritoryId = TerritoryId,
      CompanyId = CompanyId,
      ProvinceId = ProvinceId,
      Code = "WP-T-01",
      Name = "Colombo North",
      Status = "Active",
      CreatedAt = TerritoryCreatedAt
    };
  }

  private static TerritoryAgencyAssignment CreateTerritoryAssignment()
  {
    return new TerritoryAgencyAssignment
    {
      AssignmentId = TerritoryAssignmentId,
      CompanyId = CompanyId,
      TerritoryId = TerritoryId,
      AgencyId = AgencyId,
      StartsAt = TerritoryAssignmentStartsAt
    };
  }

  private static Shop CreateShop(DateTimeOffset? updatedAt)
  {
    return new Shop
    {
      ShopId = ShopId,
      CompanyId = CompanyId,
      TerritoryId = TerritoryId,
      Name = "Perera Stores",
      Address = "12 Main Street, Colombo",
      Status = "Active",
      CreatedAt = ShopCreatedAt,
      UpdatedAt = updatedAt
    };
  }

  private static SalesRepTerritoryAssignment CreateSalesRepAssignment()
  {
    return new SalesRepTerritoryAssignment
    {
      AssignmentId = SalesRepAssignmentId,
      CompanyId = CompanyId,
      TerritoryId = TerritoryId,
      SalesRepId = SalesRepId,
      StartsAt = SalesRepAssignmentStartsAt
    };
  }
}
