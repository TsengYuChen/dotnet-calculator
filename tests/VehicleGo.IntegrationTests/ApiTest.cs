using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using VehicleGo.IntegrationTests.Infrastructure;

namespace VehicleGo.IntegrationTests;

/// <summary>Base class of the API tests: shared app (<see cref="VehicleGoFactory"/>), helpers for users and vehicles.</summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTest(VehicleGoFactory factory)
{
    protected VehicleGoFactory Factory { get; } = factory;

    protected Database Db { get; } = new(factory.Postgres.ConnectionString);

    protected static string NewPlate() => $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();

    protected Task<TestClient> NewUserAsync(string? email = null) => TestClient.LoginAsync(Factory, email);

    /// <summary>POST /api/vehicles, asserting 201; returns the VehicleDto.</summary>
    protected static async Task<JsonElement> CreateAsync(TestClient client, JsonObject input)
    {
        ArgumentNullException.ThrowIfNull(client);
        var response = await client.PostAsync("/api/vehicles", input);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Create failed: {(int)response.StatusCode} {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    protected static Guid IdOf(JsonElement vehicle) => vehicle.GetProperty("id").GetGuid();

    protected static uint VersionOf(JsonElement vehicle) => vehicle.GetProperty("version").GetUInt32();

    protected static async Task TrashAsync(TestClient client, Guid id)
    {
        ArgumentNullException.ThrowIfNull(client);
        var response = await client.PostAsync($"/api/vehicles/{id}/trash");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    protected static JsonElement Inspection(JsonElement vehicle, string kind) =>
        Assert.Single(vehicle.GetProperty("inspections").EnumerateArray(), i => i.GetProperty("kind").GetString() == kind);

    protected static void AssertInspection(
        JsonElement inspection, string status, string? earliest, string? deadline, string basis, bool estimated, string? ruleId)
    {
        Assert.Equal(status, inspection.GetProperty("status").GetString());
        Assert.Equal(earliest, inspection.GetProperty("earliestDate").GetString());
        Assert.Equal(deadline, inspection.GetProperty("deadline").GetString());
        Assert.Equal(basis, inspection.GetProperty("basis").GetString());
        Assert.Equal(estimated, inspection.GetProperty("estimated").GetBoolean());
        Assert.Equal(ruleId, inspection.GetProperty("ruleId").GetString());
    }
}
