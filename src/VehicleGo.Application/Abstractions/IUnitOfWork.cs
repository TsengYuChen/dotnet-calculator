using VehicleGo.Domain.Common;

namespace VehicleGo.Application.Abstractions;

/// <summary>Runs several repository steps in one database transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Opens a transaction, runs <paramref name="work"/> and commits when it returns success;
    /// rolls back when it returns a failure (or throws). Only opens / commits / rolls back.
    /// </summary>
    Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken);
}
