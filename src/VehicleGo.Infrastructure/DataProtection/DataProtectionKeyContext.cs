using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace VehicleGo.Infrastructure.DataProtection;

/// <summary>Data Protection key ring storage (<c>data_protection_keys</c>), separate from <c>AppDbContext</c>.</summary>
public sealed class DataProtectionKeyContext(DbContextOptions<DataProtectionKeyContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public const string MigrationsHistoryTable = "__ef_dp_migrations_history";

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
