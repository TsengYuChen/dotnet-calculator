using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Vehicles;

/// <summary>A trashed vehicle (design §1.4); <see cref="PurgeAt"/> = <see cref="DeletedAt"/> + 30 days.</summary>
public sealed record TrashItemDto(
    Guid Id,
    string Plate,
    string Brand,
    string Model,
    string? Nickname,
    VehicleType VehicleType,
    DateTimeOffset DeletedAt,
    DateTimeOffset PurgeAt);
