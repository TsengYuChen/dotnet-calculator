using Microsoft.EntityFrameworkCore;
using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Infrastructure.Persistence.Queries;

/// <summary><see cref="IVehicleQueries"/>: read-only (<c>AsNoTracking</c>), limited by the <c>Owner</c> filter.</summary>
public sealed class VehicleQueries(AppDbContext db) : IVehicleQueries
{
    public async Task<IReadOnlyList<Vehicle>> ListActiveAsync(CancellationToken cancellationToken) =>
        await db.Vehicles.AsNoTracking().ToListAsync(cancellationToken);

    public Task<Vehicle?> GetActiveAsync(Guid id, CancellationToken cancellationToken) =>
        db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListTrashAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        await db.Vehicles
            .IgnoreQueryFilters([AppDbContext.DeletedFilter])
            .AsNoTracking()
            .Where(v => v.DeletedAt != null && v.DeletedAt > cutoff)
            .ToListAsync(cancellationToken);
}
