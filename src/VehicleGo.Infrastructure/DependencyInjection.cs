using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VehicleGo.Application.Abstractions;
using VehicleGo.Infrastructure.Crypto;
using VehicleGo.Infrastructure.DataProtection;
using VehicleGo.Infrastructure.Persistence;
using VehicleGo.Infrastructure.Persistence.Queries;
using VehicleGo.Infrastructure.Persistence.Repositories;

namespace VehicleGo.Infrastructure;

public static class DependencyInjection
{
    /// <summary><c>ConnectionStrings:Default</c> (<c>ConnectionStrings__Default</c> in the environment).</summary>
    public const string ConnectionStringName = "Default";

    public const string ApplicationName = "VehicleGo";

    /// <summary>
    /// Registers the DbContexts, repositories, query service, unit of work, crypto and Data Protection.
    /// Configuration values are read when first needed, so tests can replace them. <see cref="ICurrentUser"/>
    /// is registered by Web.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);

        // Crypto
        services.AddSingleton(_ => MasterKey.Parse(configuration[MasterKey.ConfigurationKey]));
        services.AddSingleton<IEmailProtector, EmailProtector>();

        // Persistence (no DbContext pooling: AppDbContext holds the current user)
        services.AddDbContext<AppDbContext>(options => AppDbContext.Configure(options, ConnectionString(configuration)));
        services.AddDbContext<DataProtectionKeyContext>(options => DataProtectionKeyContext.Configure(options, ConnectionString(configuration)));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVehicleQueries, VehicleQueries>();
        services.TryAddSingleton<IRuleTableProvider, RuleTableProvider>();

        // Data Protection: keys in PostgreSQL, encrypted with AES-GCM
        services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<DataProtectionKeyContext>();
        services.AddOptions<KeyManagementOptions>()
            .Configure<MasterKey>((options, masterKey) => options.XmlEncryptor = new AesGcmXmlEncryptor(masterKey));

        return services;
    }

    private static string ConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is not configured.");
}
