using VehicleGo.Domain.Common;
using VehicleGo.Domain.Inspections;

namespace VehicleGo.Domain.Vehicles;

public sealed class Vehicle
{
    private Vehicle()
    {
        Plate = string.Empty;
        NormalizedPlate = string.Empty;
        Brand = string.Empty;
        Model = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Plate { get; private set; }
    public string NormalizedPlate { get; private set; }
    public string Brand { get; private set; }
    public string Model { get; private set; }
    public string? Nickname { get; private set; }
    public string? Color { get; private set; }
    public VehicleType VehicleType { get; private set; }
    public DisplacementBracket? DisplacementBracket { get; private set; }
    public DateOnly ManufactureDate { get; private set; }
    public DateOnly OriginalIssueDate { get; private set; }
    public DateOnly? LicenceInspectionDate { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>); set by the persistence layer.</summary>
    public uint Version { get; private set; }

    public bool IsTrashed => DeletedAt is not null;

    /// <summary>Validates (design §1.5) and creates a vehicle with a normalised plate.</summary>
    public static Result<Vehicle> Create(Guid id, Guid ownerId, VehicleDetails details, DateOnly today, DateTimeOffset now)
    {
        var validation = VehicleValidator.Validate(details, today);
        if (validation.IsFailure)
        {
            return Result<Vehicle>.Failure(validation.Error);
        }

        var vehicle = new Vehicle { Id = id, OwnerId = ownerId, CreatedAt = now };
        vehicle.Apply(details, now);
        return Result<Vehicle>.Success(vehicle);
    }

    /// <summary>Full replacement of the user-editable data; nothing changes when validation fails.</summary>
    public Result Update(VehicleDetails details, DateOnly today, DateTimeOffset now)
    {
        if (IsTrashed)
        {
            return Result.Failure(ErrorCode.VehicleInTrash);
        }

        var validation = VehicleValidator.Validate(details, today);
        if (validation.IsFailure)
        {
            return validation;
        }

        Apply(details, now);
        return Result.Success();
    }

    public Result Trash(DateTimeOffset now)
    {
        if (IsTrashed)
        {
            return Result.Failure(ErrorCode.VehicleInTrash);
        }

        DeletedAt = now;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Restore(DateTimeOffset now)
    {
        if (!IsTrashed)
        {
            return Result.Failure(ErrorCode.VehicleNotInTrash);
        }

        DeletedAt = null;
        UpdatedAt = now;
        return Result.Success();
    }

    private void Apply(VehicleDetails details, DateTimeOffset now)
    {
        Plate = details.Plate;
        NormalizedPlate = PlateNormalizer.Normalize(details.Plate);
        Brand = details.Brand;
        Model = details.Model;
        Nickname = details.Nickname;
        Color = details.Color;
        VehicleType = details.VehicleType;
        DisplacementBracket = details.DisplacementBracket;
        ManufactureDate = details.ManufactureDate;
        OriginalIssueDate = details.OriginalIssueDate;
        LicenceInspectionDate = details.LicenceInspectionDate;
        UpdatedAt = now;
    }

    public InspectionInput ToInspectionInput() =>
        new(VehicleType, DisplacementBracket, ManufactureDate, OriginalIssueDate, LicenceInspectionDate);
}
