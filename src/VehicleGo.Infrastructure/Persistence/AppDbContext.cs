using Microsoft.EntityFrameworkCore;
using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Users;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Infrastructure.Persistence;

/// <summary>
/// <c>users</c> and <c>vehicles</c> (design §1.6). <see cref="Vehicle"/> has two named query filters:
/// <see cref="OwnerFilter"/> (current user, from <see cref="ICurrentUser"/>; <see cref="Guid.Empty"/> matches
/// nothing) and <see cref="DeletedFilter"/> (not in the trash). Trash queries switch off only
/// <see cref="DeletedFilter"/>. No DbContext pooling (the context holds the current user).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public const string OwnerFilter = "Owner";
    public const string DeletedFilter = "Deleted";
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    /// <summary>Referenced by the <see cref="OwnerFilter"/>; EF Core evaluates it per query.</summary>
    public Guid CurrentUserId => currentUser.UserId;

    /// <summary>Npgsql, snake_case names and the migration history table (shared by DI and the design-time factory).</summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        foreach (var enumType in typeof(Vehicle).Assembly.GetTypes().Where(CodeEnums.IsCodeEnum))
        {
            configurationBuilder.Properties(enumType)
                .HaveConversion(typeof(CodeValueConverter<>).MakeGenericType(enumType));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new Configurations.UserConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.VehicleConfiguration());

        modelBuilder.Entity<Vehicle>().HasQueryFilter(OwnerFilter, v => v.OwnerId == CurrentUserId);
        modelBuilder.Entity<Vehicle>().HasQueryFilter(DeletedFilter, v => v.DeletedAt == null);
    }
}
