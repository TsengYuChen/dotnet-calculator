using VehicleGo.Application.Abstractions;

namespace VehicleGo.UnitTests.Fakes;

public sealed class FakeCurrentUser : ICurrentUser
{
    public static readonly Guid DefaultUserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public Guid UserId { get; set; } = DefaultUserId;

    public string? Email { get; set; } = "alice@example.com";
}
