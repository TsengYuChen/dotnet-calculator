using VehicleGo.Domain.Common;

namespace VehicleGo.Application.Abstractions;

/// <summary>A read use case (D-T3). One implementation per use case.</summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
