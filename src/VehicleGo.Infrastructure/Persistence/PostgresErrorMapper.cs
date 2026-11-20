using Microsoft.EntityFrameworkCore;
using Npgsql;
using VehicleGo.Domain.Common;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>
/// Database errors that are only fallbacks for concurrent writes (design §2.4). The handlers check plates and
/// versions before saving; these mappings catch the race between check and save.
/// </summary>
public static class PostgresErrorMapper
{
    public const string VehiclePlateUniqueIndex = "ux_vehicles_owner_plate";

    /// <summary>The error code, or null when the error is unexpected (the caller rethrows).</summary>
    public static Error? TryMap(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is DbUpdateConcurrencyException)
        {
            return new Error(ErrorCode.VehicleModified);
        }

        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: VehiclePlateUniqueIndex })
        {
            return new Error(ErrorCode.PlateExistsActive);
        }

        return null;
    }

    /// <summary><c>SaveChangesAsync</c> with the mapping above; unexpected errors are rethrown.</summary>
    internal static async Task<Result> SaveChangesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (TryMap(ex) is { } error)
        {
            return Result.Failure(error);
        }
    }
}
