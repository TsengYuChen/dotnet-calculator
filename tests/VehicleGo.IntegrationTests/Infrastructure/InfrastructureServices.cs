using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using VehicleGo.Application.Abstractions;
using VehicleGo.Infrastructure;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>Builds a service provider with <c>AddInfrastructure</c>, a fake current user and a fake clock.</summary>
internal static class InfrastructureServices
{
    /// <summary>A random MASTER_KEY generated at run time (never a fixed value).</summary>
    public static string RandomMasterKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static ServiceProvider Build(string connectionString, FakeCurrentUser currentUser, FakeTimeProvider? time = null, string? masterKey = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["MASTER_KEY"] = masterKey ?? RandomMasterKey(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(time ?? new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 1, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<ICurrentUser>(currentUser);
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; }

    public string? Email { get; set; } = "smoke@example.com";
}
