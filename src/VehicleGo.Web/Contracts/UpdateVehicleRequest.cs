using System.ComponentModel.DataAnnotations;

namespace VehicleGo.Web.Contracts;

/// <summary><c>UpdateVehicleRequest</c> = <see cref="VehicleInput"/> + <c>version</c> (the version last read).</summary>
public sealed class UpdateVehicleRequest : VehicleInput
{
    [Required]
    public uint? Version { get; init; }
}
