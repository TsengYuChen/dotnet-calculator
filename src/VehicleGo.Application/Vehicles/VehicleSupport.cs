using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Vehicles;

/// <summary>Steps shared by the vehicle handlers.</summary>
internal static class VehicleSupport
{
    /// <summary>Trashed on or before this instant = 30 days in the trash (design §1.6).</summary>
    public static DateTimeOffset TrashCutoff(DateTimeOffset now) => now.AddDays(-VehicleLimits.TrashRetentionDays);

    /// <summary>
    /// Design §2.3 step 5: PLATE_EXISTS_ACTIVE / PLATE_EXISTS_IN_TRASH (with <c>trashedVehicleId</c>) when
    /// another vehicle (not <paramref name="self"/>) has the plate; null when the plate is free.
    /// </summary>
    public static async Task<Error?> CheckPlateAsync(
        IVehicleRepository vehicles,
        string normalizedPlate,
        Guid? self,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var existing = await vehicles.FindByNormalizedPlateAsync(normalizedPlate, cutoff, cancellationToken);
        if (existing is null || existing.Id == self)
        {
            return null;
        }

        return existing.IsTrashed
            ? new Error(ErrorCode.PlateExistsInTrash) { Extensions = new Dictionary<string, object> { ["trashedVehicleId"] = existing.Id } }
            : new Error(ErrorCode.PlateExistsActive);
    }

    public static Error LimitReached(int active, int trash) =>
        new(ErrorCode.VehicleLimitReached)
        {
            Extensions = new Dictionary<string, object> { ["activeCount"] = active, ["trashCount"] = trash },
        };
}
