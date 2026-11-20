using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VehicleGo.Application.Abstractions;
using VehicleGo.Web.Auth;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>
/// The real Web app on a Testcontainers PostgreSQL: <c>IntegrationTest</c> environment, an in-memory
/// random <c>MASTER_KEY</c>, a replaceable allowlist, a <see cref="FakeTimeProvider"/> fixed to
/// 2026-11-19 (Asia/Taipei), a replaceable rule table, captured logs and <see cref="TestLoginEndpoint"/>.
/// Google's OIDC metadata is supplied statically, so no test calls Google.
/// </summary>
public sealed class VehicleGoFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Environment = "IntegrationTest";
    public const string GitSha = "test-sha";

    /// <summary>2026-11-19 10:00 in Asia/Taipei (02:00 UTC).</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(8));

    private readonly string _masterKey = InfrastructureServices.RandomMasterKey();

    public PostgresFixture Postgres { get; } = new();

    /// <summary>UTC like <see cref="TimeProvider.System"/> (Npgsql writes only offset-0 timestamptz values).</summary>
    public FakeTimeProvider Time { get; } = new(Now.ToUniversalTime());

    public TestAllowlist Allowlist { get; } = new();

    public SwappableRuleTableProvider Rules { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await Postgres.InitializeAsync();
        _ = Server; // start the app (runs the start-up validation and migrations)
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Postgres.DisposeAsync();
        Logs.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment(Environment);
        builder.UseSetting("ConnectionStrings:Default", Postgres.ConnectionString);
        builder.UseSetting("MASTER_KEY", _masterKey);
        builder.UseSetting("ALLOWED_EMAILS", "seed@example.com");
        builder.UseSetting("GOOGLE_CLIENT_ID", "test-client-id");
        builder.UseSetting("GOOGLE_CLIENT_SECRET", "test-client-secret-not-real");
        builder.UseSetting("GIT_SHA", GitSha);
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<IAllowlistProvider>();
            services.AddSingleton<IAllowlistProvider>(Allowlist);
            services.RemoveAll<IRuleTableProvider>();
            services.AddSingleton<IRuleTableProvider>(Rules);
            services.AddSingleton<IStartupFilter, TestLoginEndpoint>();

            services.PostConfigure<OpenIdConnectOptions>(AuthenticationSetup.OidcScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://accounts.google.com",
                    AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                    TokenEndpoint = "https://oauth2.googleapis.com/token",
                };
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }
}

/// <summary>All API tests share one app and database; they run one at a time and use their own users.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<VehicleGoFactory>
{
    public const string Name = "API";
}
