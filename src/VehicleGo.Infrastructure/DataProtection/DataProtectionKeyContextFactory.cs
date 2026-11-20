using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VehicleGo.Infrastructure.Persistence;

namespace VehicleGo.Infrastructure.DataProtection;

/// <summary>Design-time factory for <c>dotnet ef</c>; same fake connection string as <see cref="AppDbContextFactory"/>.</summary>
public sealed class DataProtectionKeyContextFactory : IDesignTimeDbContextFactory<DataProtectionKeyContext>
{
    public DataProtectionKeyContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<DataProtectionKeyContext>();
        DataProtectionKeyContext.Configure(builder, AppDbContextFactory.DesignTimeConnectionString);
        return new DataProtectionKeyContext(builder.Options);
    }
}
