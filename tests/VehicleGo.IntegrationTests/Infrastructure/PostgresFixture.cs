using Testcontainers.PostgreSql;
using VehicleGo.Infrastructure.Persistence;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>
/// Starts PostgreSQL (tag from <c>postgres-image.txt</c>) and applies the migrations of both DbContexts.
/// Needs Docker. Retries the start with back-off (Docker Hub can answer 429 while pulling).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const int MaxAttempts = 5;

    private PostgreSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("PostgreSQL has not been started.");

    public static string ImageTag()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "postgres-image.txt");
            if (File.Exists(file))
            {
                return File.ReadAllText(file).Trim();
            }
        }

        throw new FileNotFoundException("postgres-image.txt not found above the test output directory.");
    }

    public async ValueTask InitializeAsync()
    {
        for (var attempt = 1; ; attempt++)
        {
            var container = new PostgreSqlBuilder(ImageTag()).Build();
            try
            {
                await container.StartAsync();
                _container = container;
                break;
            }
            catch (Exception) when (attempt < MaxAttempts)
            {
                await container.DisposeAsync();
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
            }
        }

        await using var provider = InfrastructureServices.Build(ConnectionString, new FakeCurrentUser { UserId = Guid.Empty });
        await DatabaseMigrator.MigrateAsync(provider, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
