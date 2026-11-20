namespace VehicleGo.Domain.Vehicles;

public static class VehicleLimits
{
    /// <summary>Maximum vehicles per user, trash included (G2).</summary>
    public const int MaxVehiclesPerOwner = 50;

    /// <summary>Days a trashed vehicle stays visible before it is purged (G3).</summary>
    public const int TrashRetentionDays = 30;

    public const int PlateMaxLength = 16;
    public const int TextMaxLength = 50;
}
