using System.Net;
using System.Text.Json.Nodes;
using VehicleGo.IntegrationTests.Infrastructure;

namespace VehicleGo.IntegrationTests;

/// <summary>CSRF (IAntiforgery, header X-XSRF-TOKEN): checked after the login check on every non-GET /api request.</summary>
public sealed class CsrfTests(VehicleGoFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task 沒帶header回CSRF_INVALID_ProblemDetails_不建立車輛()
    {
        using var client = await NewUserAsync();
        client.SendXsrfToken = false;

        var response = await client.PostAsync("/api/vehicles", Inputs.Car(NewPlate()));

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, "CSRF_INVALID");
        Assert.Empty((await client.GetJsonAsync("/api/vehicles")).EnumerateArray());
    }

    [Fact]
    public async Task 錯誤的token回CSRF_INVALID()
    {
        using var client = await NewUserAsync();
        client.XsrfTokenOverride = "not-a-token";

        await Problems.AssertAsync(await client.PostAsync("/api/vehicles", Inputs.Car(NewPlate())), HttpStatusCode.BadRequest, "CSRF_INVALID");
    }

    [Fact]
    public async Task 別人的token不能用()
    {
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        bob.XsrfTokenOverride = alice.XsrfCookie;

        await Problems.AssertAsync(await bob.PostAsync("/api/vehicles", Inputs.Car(NewPlate())), HttpStatusCode.BadRequest, "CSRF_INVALID");
    }

    [Fact]
    public async Task 未登入時先回401()
    {
        using var client = TestClient.Anonymous(Factory);

        var response = await client.PostAsync("/api/vehicles", Inputs.Car(NewPlate()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 每個非GET端點都檢查CSRF()
    {
        using var client = await NewUserAsync();
        var vehicle = await CreateAsync(client, Inputs.Car(NewPlate()));
        var id = IdOf(vehicle);
        client.SendXsrfToken = false;

        await Problems.AssertAsync(await client.PutAsync($"/api/vehicles/{id}", Inputs.Update(Inputs.Car(NewPlate()), VersionOf(vehicle))), HttpStatusCode.BadRequest, "CSRF_INVALID");
        await Problems.AssertAsync(await client.PostAsync($"/api/vehicles/{id}/trash"), HttpStatusCode.BadRequest, "CSRF_INVALID");
        await Problems.AssertAsync(await client.PostAsync($"/api/vehicles/{id}/restore"), HttpStatusCode.BadRequest, "CSRF_INVALID");
        await Problems.AssertAsync(await client.DeleteAsync($"/api/vehicles/{id}"), HttpStatusCode.BadRequest, "CSRF_INVALID");
        await Problems.AssertAsync(await client.PatchAsync("/api/me/preferences", new JsonObject { ["calendar"] = "GREGORIAN" }), HttpStatusCode.BadRequest, "CSRF_INVALID");
        await Problems.AssertAsync(await client.PostAsync("/api/auth/logout"), HttpStatusCode.BadRequest, "CSRF_INVALID");

        var unchanged = await client.GetJsonAsync($"/api/vehicles/{id}");
        Assert.Equal(VersionOf(vehicle), VersionOf(unchanged));
        Assert.Equal("ROC", (await client.GetJsonAsync("/api/me")).GetProperty("calendar").GetString());
    }

    [Fact]
    public async Task GET_api_me發出XSRF_TOKEN_cookie_不是HttpOnly()
    {
        using var client = await NewUserAsync();

        var response = await client.GetAsync("/api/me");

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
