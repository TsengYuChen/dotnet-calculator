using System.Net;
using VehicleGo.IntegrationTests.Infrastructure;

namespace VehicleGo.IntegrationTests;

/// <summary>CUJ 8: at most 50 vehicles per user, including the trash; exact under concurrency.</summary>
public sealed class VehicleLimitTests(VehicleGoFactory factory) : ApiTest(factory)
{
    private static async Task<List<Guid>> CreateManyAsync(TestClient client, int count)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            ids.Add(IdOf(await CreateAsync(client, Inputs.Car($"LIM-{i:D4}"))));
        }

        return ids;
    }

    [Fact]
    public async Task CUJ8_第51台被擋下_附使用中和垃圾桶的數量()
    {
        using var client = await NewUserAsync();
        var ids = await CreateManyAsync(client, 50);
        foreach (var id in ids.Take(3))
        {
            await TrashAsync(client, id);
        }

        var problem = await Problems.AssertAsync(await client.PostAsync("/api/vehicles", Inputs.Car("LIM-9999")), HttpStatusCode.Conflict, "VEHICLE_LIMIT_REACHED");

        Assert.Equal(47, problem.GetProperty("activeCount").GetInt32());
        Assert.Equal(3, problem.GetProperty("trashCount").GetInt32());
    }

    [Fact]
    public async Task 永久刪除後可以再新增()
    {
        using var client = await NewUserAsync();
        var ids = await CreateManyAsync(client, 50);
        await TrashAsync(client, ids[0]);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/vehicles/{ids[0]}")).StatusCode);

        await CreateAsync(client, Inputs.Car("LIM-9999"));
    }

    [Fact]
    public async Task 有49台時同時送出兩筆新增_只有一筆成功_另一筆回VEHICLE_LIMIT_REACHED()
    {
        using var client = await NewUserAsync();
        await CreateManyAsync(client, 49);

        var responses = await Task.WhenAll(
            client.PostAsync("/api/vehicles", Inputs.Car("RACE-0001")),
            client.PostAsync("/api/vehicles", Inputs.Car("RACE-0002")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var problem = await Problems.AssertAsync(
            Assert.Single(responses, r => r.StatusCode != HttpStatusCode.Created), HttpStatusCode.Conflict, "VEHICLE_LIMIT_REACHED");
        Assert.Equal(50, problem.GetProperty("activeCount").GetInt32());
        Assert.Equal(50, (await client.GetJsonAsync("/api/vehicles")).GetArrayLength());
    }
}
