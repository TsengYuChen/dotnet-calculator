using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>
/// An HTTP client with a real cookie jar. <see cref="LoginAsync"/> signs in through
/// <see cref="TestLoginEndpoint"/> (a <b>real</b> login cookie, validated on every request with the
/// allowlist), then calls <c>/api/me</c> for the XSRF token, which is sent automatically as
/// <c>X-XSRF-TOKEN</c> on every non-GET request while <see cref="SendXsrfToken"/> is true.
/// </summary>
public sealed class TestClient : IDisposable
{
    public static readonly Uri BaseAddress = new("http://localhost");

    private readonly HttpClient _http;

    private TestClient(VehicleGoFactory factory, string email, string subject)
    {
        Email = email;
        Subject = subject;
        _http = factory.CreateDefaultClient(BaseAddress, new XsrfHandler(this), new CookieContainerHandler(Cookies));
    }

    public CookieContainer Cookies { get; } = new();

    public string Email { get; }

    public string Subject { get; }

    public bool SendXsrfToken { get; set; } = true;

    /// <summary>When set, sent instead of the XSRF-TOKEN cookie value.</summary>
    public string? XsrfTokenOverride { get; set; }

    public string? XsrfCookie => Cookies.GetCookies(BaseAddress)["XSRF-TOKEN"]?.Value;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string NewEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@example.com";

    /// <summary>A client with no login cookie.</summary>
    public static TestClient Anonymous(VehicleGoFactory factory) => new(factory, string.Empty, string.Empty);

    /// <summary>
    /// Adds the e-mail to the allowlist (unless <paramref name="allow"/> is false), signs in and fetches
    /// the XSRF token. Throws when the login is rejected; use <see cref="TryLoginAsync"/> to test rejections.
    /// </summary>
    public static async Task<TestClient> LoginAsync(VehicleGoFactory factory, string? email = null, string? subject = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        email ??= NewEmail();
        factory.Allowlist.Add(email);
        var client = new TestClient(factory, email, subject ?? $"google-{Guid.NewGuid():N}");
        var login = await client.TryLoginAsync();
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var me = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.NotNull(client.XsrfCookie);
        return client;
    }

    /// <summary>A client that has not signed in yet, for <see cref="TryLoginAsync"/>.</summary>
    public static TestClient Create(VehicleGoFactory factory, string email, string subject) => new(factory, email, subject);

    public Task<HttpResponseMessage> TryLoginAsync(bool emailVerified = true) =>
        _http.PostAsync(
            $"{TestLoginEndpoint.Path}?sub={Uri.EscapeDataString(Subject)}&email={Uri.EscapeDataString(Email)}&emailVerified={(emailVerified ? "true" : "false")}",
            null,
            Ct);

    public Task<HttpResponseMessage> GetAsync(string url) => _http.GetAsync(url, Ct);

    public Task<HttpResponseMessage> PostAsync(string url, JsonNode? body = null) => _http.PostAsync(url, Json(body), Ct);

    public Task<HttpResponseMessage> PutAsync(string url, JsonNode body) => _http.PutAsync(url, Json(body), Ct);

    public Task<HttpResponseMessage> PatchAsync(string url, JsonNode body) => _http.PatchAsync(url, Json(body), Ct);

    public Task<HttpResponseMessage> DeleteAsync(string url) => _http.DeleteAsync(url, Ct);

    /// <summary>Sends raw JSON text (for malformed bodies).</summary>
    public Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, string json) =>
        _http.SendAsync(new HttpRequestMessage(method, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") }, Ct);

    public async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = await response.Content.ReadAsStringAsync(Ct);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    public void Dispose() => _http.Dispose();

    private static StringContent? Json(JsonNode? body) =>
        body is null ? null : new StringContent(body.ToJsonString(), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

    private sealed class XsrfHandler(TestClient owner) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var safe = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
            var token = owner.XsrfTokenOverride ?? owner.XsrfCookie;
            if (!safe && owner.SendXsrfToken && token is not null)
            {
                request.Headers.Add("X-XSRF-TOKEN", token);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
