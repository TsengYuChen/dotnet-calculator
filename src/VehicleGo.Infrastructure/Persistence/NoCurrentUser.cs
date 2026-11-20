using VehicleGo.Application.Abstractions;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>Nobody signed in (<see cref="Guid.Empty"/>): the owner filter matches no vehicle. Used at design time.</summary>
internal sealed class NoCurrentUser : ICurrentUser
{
    public Guid UserId => Guid.Empty;

    public string? Email => null;
}
