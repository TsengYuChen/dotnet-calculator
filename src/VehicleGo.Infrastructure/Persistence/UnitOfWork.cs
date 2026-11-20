using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>
/// One database transaction around <c>work</c> (<c>BeginTransactionAsync</c>). Npgsql's
/// <c>EnableRetryOnFailure</c> is not used: a user-initiated transaction cannot be combined with automatic retries.
/// </summary>
public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        Result<T> result;
        try
        {
            result = await work(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }

        if (result.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);

            // Entities saved inside the rolled-back transaction no longer match the database.
            db.ChangeTracker.Clear();
        }

        return result;
    }
}
