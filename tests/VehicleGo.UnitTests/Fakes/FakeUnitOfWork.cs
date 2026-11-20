using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;

namespace VehicleGo.UnitTests.Fakes;

/// <summary>Records BEGIN / COMMIT / ROLLBACK in the shared <see cref="CallLog"/>.</summary>
public sealed class FakeUnitOfWork(CallLog log) : IUnitOfWork
{
    public int Executions { get; private set; }

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken)
    {
        Executions++;
        log.Add("BEGIN");
        Result<T> result;
        try
        {
            result = await work(cancellationToken);
        }
        catch
        {
            log.Add("ROLLBACK");
            throw;
        }

        log.Add(result.IsSuccess ? "COMMIT" : "ROLLBACK");
        return result;
    }
}
