using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VehicleGo.IntegrationTests.Infrastructure;
using VehicleGo.Web.Auth;

namespace VehicleGo.IntegrationTests;

/// <summary>G1, CUJ 1, 2, 10: sign-in, allowlist (checked on every request), returnUrl, logout.</summary>
public sealed class AuthTests(VehicleGoFactory factory) : ApiTest(factory)
{
    [Theory]
    [InlineData("GET", "/api/me")]
    [InlineData("GET", "/api/vehicles")]
    [InlineData("GET", "/api/trash")]
    [InlineData("GET", "/api/reference/displacement-brackets")]
    [InlineData("POST", "/api/vehicles")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("PATCH", "/api/me/preferences")]
    public async Task Api未登入回401_不轉址(string method, string url)
    {
        using var client = TestClient.Anonymous(Factory);

        var response = await client.SendRawAsync(new HttpMethod(method), url, "{}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task CUJ1_第一次登入_建立使用者_Email加密_曆法預設ROC_列表是空的()
    {
        var email = TestClient.NewEmail("first");
        using var client = await TestClient.LoginAsync(Factory, email);

        Assert.Equal(1, await Db.UsersWithSubAsync(client.Subject));
        Assert.Equal(0, await Db.CountAsync(
            "SELECT count(*) FROM users WHERE google_sub = @s AND position(convert_to(@e, 'UTF8') in email_encrypted) > 0",
            ("s", client.Subject), ("e", email)));
        var me = await client.GetJsonAsync("/api/me");
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("ROC", me.GetProperty("calendar").GetString());
        Assert.Empty((await client.GetJsonAsync("/api/vehicles")).EnumerateArray());
    }

    [Fact]
    public async Task CUJ2_不在名單上_被拒絕_不建立使用者()
    {
        using var client = TestClient.Create(Factory, TestClient.NewEmail("stranger"), $"google-{Guid.NewGuid():N}");

        var response = await client.TryLoginAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await Db.UsersWithSubAsync(client.Subject));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task email_verified為false_被拒絕_不建立使用者()
    {
        var email = TestClient.NewEmail("unverified");
        Factory.Allowlist.Add(email);
        using var client = TestClient.Create(Factory, email, $"google-{Guid.NewGuid():N}");

        var response = await client.TryLoginAsync(emailVerified: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await Db.UsersWithSubAsync(client.Subject));
    }

    [Fact]
    public async Task 名單比對去空白並轉小寫()
    {
        var email = TestClient.NewEmail("mixed");
        Factory.Allowlist.Add($"  {email.ToUpperInvariant()} ");
        using var client = TestClient.Create(Factory, email, $"google-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NoContent, (await client.TryLoginAsync()).StatusCode);
    }

    [Fact]
    public async Task CUJ10_移出名單後_下一個請求回401_清除cookie_再登入被拒絕()
    {
        using var client = await NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/vehicles")).StatusCode);

        Factory.Allowlist.Remove(client.Email);
        var response = await client.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(AuthenticationSetup.CookieName + "=;", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.TryLoginAsync()).StatusCode);
    }

    [Fact]
    public async Task 同一帳號第一次登入的多個請求同時到達_只建立一筆使用者()
    {
        var email = TestClient.NewEmail("race");
        var subject = $"google-{Guid.NewGuid():N}";
        Factory.Allowlist.Add(email);
        var clients = Enumerable.Range(0, 6).Select(_ => TestClient.Create(Factory, email, subject)).ToList();

        var responses = await Task.WhenAll(clients.Select(c => c.TryLoginAsync()));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.Equal(1, await Db.UsersWithSubAsync(subject));
        clients.ForEach(c => c.Dispose());
    }

    [Fact]
    public async Task 登入端點不需登入_導向Google_只申請openid_email()
    {
        using var client = TestClient.Anonymous(Factory);

        var response = await client.GetAsync("/api/auth/login?returnUrl=%2Fvehicles");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("accounts.google.com", location.Host);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("openid email", query["scope"].ToString());
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Equal("test-client-id", query["client_id"].ToString());
        Assert.Equal("http://localhost/signin-oidc", query["redirect_uri"].ToString());
    }

    [Theory]
    [InlineData("/vehicles", "/vehicles")]
    [InlineData("/vehicles/new?x=1", "/vehicles/new?x=1")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example.com/", "/")]
    [InlineData("//evil.example.com", "/")]
    [InlineData("/\\evil.example.com", "/")]
    [InlineData("vehicles", "/")]
    public async Task 登入的returnUrl只接受站內相對路徑(string? returnUrl, string expected)
    {
        using var client = TestClient.Anonymous(Factory);
        var url = returnUrl is null ? "/api/auth/login" : $"/api/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var state = QueryHelpers.ParseQuery(response.Headers.Location!.Query)["state"].ToString();
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(AuthenticationSetup.OidcScheme);
        var properties = options.StateDataFormat.Unprotect(state);
        Assert.Equal(expected, properties?.RedirectUri);
    }

    [Fact]
    public async Task 登出要CSRF_成功後回204_cookie失效()
    {
        using var client = await NewUserAsync();

        client.SendXsrfToken = false;
        await Problems.AssertAsync(await client.PostAsync("/api/auth/logout"), HttpStatusCode.BadRequest, "CSRF_INVALID");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);

        client.SendXsrfToken = true;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task 未登入的api回應是ProblemDetails()
    {
        using var client = TestClient.Anonymous(Factory);

        var response = await client.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task 不存在的api路徑不會回SPA的index_html()
    {
        using var client = await NewUserAsync();

        var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task 修改曆法_CUJ11_換一個登入仍是西元()
    {
        var email = TestClient.NewEmail("calendar");
        var subject = $"google-{Guid.NewGuid():N}";
        using var first = TestClient.Create(Factory, email, subject);
        Factory.Allowlist.Add(email);
        Assert.Equal(HttpStatusCode.NoContent, (await first.TryLoginAsync()).StatusCode);
        await first.GetAsync("/api/me");

        var patched = await first.PatchAsync("/api/me/preferences", new JsonObject { ["calendar"] = "GREGORIAN" });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var body = await TestClient.ReadJsonAsync(patched);
        Assert.Equal("GREGORIAN", body.GetProperty("calendar").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());

        using var second = await TestClient.LoginAsync(Factory, email, subject);
        Assert.Equal("GREGORIAN", (await second.GetJsonAsync("/api/me")).GetProperty("calendar").GetString());
    }
}
