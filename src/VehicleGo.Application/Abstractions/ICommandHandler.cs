using VehicleGo.Domain.Common;

namespace VehicleGo.Application.Abstractions;

/// <summary>A write use case without a result value (D-T3). One implementation per use case.</summary>
public interface ICommandHandler<in TCommand>
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>A write use case that returns a value on success (D-T3), e.g. the created vehicle.</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
