using System.Net;
using VehicleGo.IntegrationTests.Infrastructure;
using VehicleGo.Web.Health;

namespace VehicleGo.IntegrationTests;

/// <summary>Health checks (no login): <c>{ status, gitSha }</c>.</summary>
public sealed class HealthTests(VehicleGoFactory factory) : ApiTest(factory)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task 健康檢查不需登入_回傳status和gitSha(string url)
    {
        using var client = TestClient.Anonymous(Factory);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await TestClient.ReadJsonAsync(response);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal(VehicleGoFactory.GitSha, body.GetProperty("gitSha").GetString());
    }

    [Theory]
    [InlineData(null, 8080)]
    [InlineData("", 8080)]
    [InlineData("5000", 5000)]
    [InlineData("8081;8082", 8081)]
    [InlineData(" 9000 ; 9001", 9000)]
    [InlineData("not-a-port", 8080)]
    public void healthcheck參數從ASPNETCORE_HTTP_PORTS取第一個port(string? ports, int expected) =>
        Assert.Equal(expected, HealthCheckCommand.ParsePort(ports));
}
