using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Vehicles;

public sealed record ListTrashQuery;

/// <summary>Trashed vehicles that are not yet 30 days in the trash, most recently trashed first.</summary>
public sealed class ListTrashQueryHandler(IVehicleQueries queries, TimeProvider timeProvider)
    : IQueryHandler<ListTrashQuery, IReadOnlyList<TrashItemDto>>
{
    public async Task<Result<IReadOnlyList<TrashItemDto>>> HandleAsync(ListTrashQuery query, CancellationToken cancellationToken)
    {
        var cutoff = VehicleSupport.TrashCutoff(timeProvider.GetUtcNow());
        var trashed = await queries.ListTrashAsync(cutoff, cancellationToken);

        IReadOnlyList<TrashItemDto> items = trashed
            .Where(v => v.DeletedAt is not null)
            .OrderByDescending(v => v.DeletedAt)
            .ThenBy(v => v.NormalizedPlate, StringComparer.Ordinal)
            .Select(v => new TrashItemDto(
                v.Id,
                v.Plate,
                v.Brand,
                v.Model,
                v.Nickname,
                v.VehicleType,
                v.DeletedAt!.Value,
                v.DeletedAt.Value.AddDays(VehicleLimits.TrashRetentionDays)))
            .ToList();
        return Result<IReadOnlyList<TrashItemDto>>.Success(items);
    }
}
