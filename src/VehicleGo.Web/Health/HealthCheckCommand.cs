using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VehicleGo.Web.Health;

/// <summary>
/// <c>dotnet VehicleGo.Web.dll --healthcheck</c> (the runtime image has no curl): calls <c>/health/ready</c>
/// on the first port of <c>ASPNETCORE_HTTP_PORTS</c> (default 8080); exit code 0 when healthy.
/// Handled before the <c>WebApplication</c> is built.
/// </summary>
public static class HealthCheckCommand
{
    public const string Argument = "--healthcheck";
    public const int DefaultPort = 8080;

    public static int ParsePort(string? httpPorts)
    {
        var first = (httpPorts ?? string.Empty).Split(';', StringSplitOptions.TrimEntries)[0];
        return int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535
            ? port
            : DefaultPort;
    }

    public static async Task<int> RunAsync(string? httpPorts)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await client.GetAsync(new Uri($"http://localhost:{ParsePort(httpPorts)}/health/ready"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            return 1;
        }
        catch (TaskCanceledException)
        {
            return 1;
        }
    }

    /// <summary>The health endpoints' body: <c>{ status, gitSha }</c>.</summary>
    public static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);
        var gitSha = context.RequestServices.GetRequiredService<IConfiguration>()["GIT_SHA"];
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            gitSha = string.IsNullOrWhiteSpace(gitSha) ? "unknown" : gitSha,
        }));
    }
}
