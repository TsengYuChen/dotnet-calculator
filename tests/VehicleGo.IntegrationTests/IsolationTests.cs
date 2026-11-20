using System.Net;
using System.Text.Json.Nodes;
using VehicleGo.IntegrationTests.Infrastructure;

namespace VehicleGo.IntegrationTests;

/// <summary>G9-3: user A can neither read nor write any of user B's data (vehicles, trash, preferences).</summary>
public sealed class IsolationTests(VehicleGoFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task A對B的每個車輛和垃圾桶端點都回404()
    {
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var bobInput = Inputs.Car(NewPlate());
        var bobActive = await CreateAsync(bob, bobInput);
        var bobTrashed = await CreateAsync(bob, Inputs.Car(NewPlate()));
        await TrashAsync(bob, IdOf(bobTrashed));

        foreach (var id in new[] { IdOf(bobActive), IdOf(bobTrashed) })
        {
            await Problems.AssertAsync(await alice.GetAsync($"/api/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
            await Problems.AssertAsync(await alice.PutAsync($"/api/vehicles/{id}", Inputs.Update(Inputs.Car(NewPlate()), VersionOf(bobActive))), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
            await Problems.AssertAsync(await alice.PostAsync($"/api/vehicles/{id}/trash"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
            await Problems.AssertAsync(await alice.PostAsync($"/api/vehicles/{id}/restore"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
            await Problems.AssertAsync(await alice.DeleteAsync($"/api/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        }

        var unchanged = await bob.GetJsonAsync($"/api/vehicles/{IdOf(bobActive)}");
        Assert.Equal(VersionOf(bobActive), VersionOf(unchanged));
        Assert.Equal([IdOf(bobTrashed)], (await bob.GetJsonAsync("/api/trash")).EnumerateArray().Select(IdOf));
    }

    [Fact]
    public async Task 列表和垃圾桶只包含自己的資料()
    {
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var aliceCar = await CreateAsync(alice, Inputs.Car(NewPlate()));
        await CreateAsync(bob, Inputs.Car(NewPlate()));
        var bobTrashed = await CreateAsync(bob, Inputs.Car(NewPlate()));
        await TrashAsync(bob, IdOf(bobTrashed));

        Assert.Equal([IdOf(aliceCar)], (await alice.GetJsonAsync("/api/vehicles")).EnumerateArray().Select(IdOf));
        Assert.Empty((await alice.GetJsonAsync("/api/trash")).EnumerateArray());
    }

    [Fact]
    public async Task A可以登錄和B相同的車牌_B的垃圾桶也不影響A()
    {
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var activePlate = NewPlate();
        var trashedPlate = NewPlate();
        await CreateAsync(bob, Inputs.Car(activePlate));
        var trashed = await CreateAsync(bob, Inputs.Car(trashedPlate));
        await TrashAsync(bob, IdOf(trashed));

        await CreateAsync(alice, Inputs.Car(activePlate));
        await CreateAsync(alice, Inputs.Car(trashedPlate));
    }

    [Fact]
    public async Task B的車不算進A的上限()
    {
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        await CreateAsync(bob, Inputs.Car(NewPlate()));

        var created = await alice.PostAsync("/api/vehicles", Inputs.Car(NewPlate()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task api_me只讀寫自己()
    {
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();

        Assert.Equal(HttpStatusCode.OK, (await alice.PatchAsync("/api/me/preferences", new JsonObject { ["calendar"] = "GREGORIAN" })).StatusCode);

        var aliceMe = await alice.GetJsonAsync("/api/me");
        var bobMe = await bob.GetJsonAsync("/api/me");
        Assert.Equal(alice.Email, aliceMe.GetProperty("email").GetString());
        Assert.Equal("GREGORIAN", aliceMe.GetProperty("calendar").GetString());
        Assert.Equal(bob.Email, bobMe.GetProperty("email").GetString());
        Assert.Equal("ROC", bobMe.GetProperty("calendar").GetString());
    }

    [Fact]
    public async Task B的垃圾桶滿30天的車不會被A的寫入清除()
    {
        using var alice = await NewUserAsync();
        using var bob = await NewUserAsync();
        var bobCar = await CreateAsync(bob, Inputs.Car(NewPlate()));
        await TrashAsync(bob, IdOf(bobCar));
        await Db.SetDeletedAtAsync(IdOf(bobCar), Factory.Time.GetUtcNow().AddDays(-31));

        await CreateAsync(alice, Inputs.Car(NewPlate()));

        Assert.Equal(1, await Db.VehicleRowsAsync(IdOf(bobCar)));
    }
}
