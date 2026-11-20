using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VehicleGo.Application;
using VehicleGo.Application.Abstractions;
using VehicleGo.Infrastructure;
using VehicleGo.Infrastructure.Persistence;
using VehicleGo.Web.Auth;
using VehicleGo.Web.Errors;
using VehicleGo.Web.Health;
using VehicleGo.Web.Security;
using VehicleGo.Web.Startup;

// Compose's container health check (the runtime image has no curl): handled before building the app.
if (args.Contains(HealthCheckCommand.Argument, StringComparer.Ordinal))
{
    return await HealthCheckCommand.RunAsync(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS"));
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();

// Dependencies (D-T1): Application handlers, Infrastructure implementations, Web adapters.
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddSingleton<IAllowlistProvider, EnvironmentAllowlistProvider>();

builder.Services.AddVehicleGoAuthentication();
builder.Services.AddAntiforgery(options => options.HeaderName = AntiforgeryFilter.HeaderName);
builder.Services.AddProblemDetails();

builder.Services
    .AddControllers(options =>
    {
        // Model validation errors use the JSON (camelCase) field names.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        options.Filters.Add<AntiforgeryFilter>();
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)));
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = ErrorCodeMapping.InvalidModelState);

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"]);

var app = builder.Build();

StartupValidation.Validate(app.Configuration, app.Logger);
await DatabaseMigrator.MigrateAsync(app.Services, app.Lifetime.ApplicationStopping);

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckCommand.WriteResponseAsync,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckCommand.WriteResponseAsync,
}).AllowAnonymous();

// SPA fallback: any other non-file path serves index.html, except /api and /signin-oidc (→ 404).
app.MapFallback(SpaFallback.HandleAsync).AllowAnonymous();

await app.RunAsync();
return 0;
