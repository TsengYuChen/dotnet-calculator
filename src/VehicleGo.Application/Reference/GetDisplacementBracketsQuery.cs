using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Application.Reference;

/// <summary>One entry of <c>GET /api/reference/displacement-brackets</c>: <c>{ code, label, vehicleTypes }</c>.</summary>
public sealed record DisplacementBracketDto(DisplacementBracket Code, string Label, IReadOnlyList<VehicleType> VehicleTypes);

public sealed record GetDisplacementBracketsQuery;

/// <summary>Reads the Domain's fixed bracket list.</summary>
public sealed class GetDisplacementBracketsQueryHandler
    : IQueryHandler<GetDisplacementBracketsQuery, IReadOnlyList<DisplacementBracketDto>>
{
    private static readonly IReadOnlyList<DisplacementBracketDto> Brackets =
        DisplacementBrackets.All.Select(b => new DisplacementBracketDto(b.Code, b.Label, b.VehicleTypes)).ToArray();

    public Task<Result<IReadOnlyList<DisplacementBracketDto>>> HandleAsync(
        GetDisplacementBracketsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result<IReadOnlyList<DisplacementBracketDto>>.Success(Brackets));
}
