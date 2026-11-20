using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleGo.Infrastructure.DataProtection;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>Applies the migrations of both DbContexts (at <c>api</c> start-up in the first version).</summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<DataProtectionKeyContext>().Database.MigrateAsync(cancellationToken);
    }
}
