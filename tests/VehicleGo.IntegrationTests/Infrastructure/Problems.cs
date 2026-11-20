using System.Net;
using System.Text.Json;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>Assertions for ProblemDetails responses (design §1.4).</summary>
public static class Problems
{
    /// <summary>Asserts status, <c>application/problem+json</c>, <c>code</c>, <c>traceId</c>; returns the body.</summary>
    public static async Task<JsonElement> AssertAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == status, $"Expected {(int)status} {code}, got {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement.Clone();
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
        Assert.True(root.TryGetProperty("title", out _));
        Assert.True(root.TryGetProperty("type", out _));
        return root;
    }

    /// <summary>VALIDATION_FAILED with exactly these camelCase <c>errors</c> keys.</summary>
    public static async Task<JsonElement> AssertValidationAsync(HttpResponseMessage response, params string[] fields)
    {
        var root = await AssertAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        var keys = root.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(fields.Order(StringComparer.Ordinal).ToArray(), keys);
        return root;
    }
}
