using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>: creates the context directly (no Web startup validation),
/// with a fake connection string (never opened) and no current user.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string DesignTimeConnectionString = "Host=localhost;Database=design_time";

    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContext.Configure(builder, DesignTimeConnectionString);
        return new AppDbContext(builder.Options, new NoCurrentUser());
    }
}
