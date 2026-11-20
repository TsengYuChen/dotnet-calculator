using System.Net;
using VehicleGo.IntegrationTests.Infrastructure;

namespace VehicleGo.IntegrationTests;

/// <summary>CUJ 9: concurrent edits (xmin version number).</summary>
public sealed class ConcurrencyTests(VehicleGoFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task CUJ9_舊版本號回VEHICLE_MODIFIED()
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);
        var url = $"/api/vehicles/{IdOf(created)}";

        var tabA = await client.PutAsync(url, Inputs.Update(input.With("nickname", "A"), VersionOf(created)));
        var tabB = await client.PutAsync(url, Inputs.Update(input.With("nickname", "B"), VersionOf(created)));

        Assert.Equal(HttpStatusCode.OK, tabA.StatusCode);
        await Problems.AssertAsync(tabB, HttpStatusCode.Conflict, "VEHICLE_MODIFIED");
        Assert.Equal("A", (await client.GetJsonAsync(url)).GetProperty("nickname").GetString());
    }

    [Fact]
    public async Task 舊版本號加重複車牌_回VEHICLE_MODIFIED()
    {
        using var client = await NewUserAsync();
        var otherPlate = NewPlate();
        await CreateAsync(client, Inputs.Car(otherPlate));
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);
        var url = $"/api/vehicles/{IdOf(created)}";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync(url, Inputs.Update(input.With("nickname", "A"), VersionOf(created)))).StatusCode);

        var stale = await client.PutAsync(url, Inputs.Update(input.With("plate", otherPlate), VersionOf(created)));

        await Problems.AssertAsync(stale, HttpStatusCode.Conflict, "VEHICLE_MODIFIED");
    }

    [Fact]
    public async Task 沒帶版本號回VALIDATION_FAILED_errors含version()
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);

        await Problems.AssertValidationAsync(await client.PutAsync($"/api/vehicles/{IdOf(created)}", input), "version");
    }

    [Theory]
    [InlineData("\"abc\"")]
    [InlineData("-1")]
    [InlineData("null")]
    public async Task 版本號格式錯誤回VALIDATION_FAILED(string rawVersion)
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);
        var json = input.ToJsonString().TrimEnd('}') + $",\"version\":{rawVersion}}}";

        var response = await client.SendRawAsync(HttpMethod.Put, $"/api/vehicles/{IdOf(created)}", json);

        await Problems.AssertValidationAsync(response, "version");
    }

    [Fact]
    public async Task 同一版本號同時送出兩筆修改_只有一筆成功()
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);
        var url = $"/api/vehicles/{IdOf(created)}";

        var responses = await Task.WhenAll(
            client.PutAsync(url, Inputs.Update(input.With("nickname", "A"), VersionOf(created))),
            client.PutAsync(url, Inputs.Update(input.With("nickname", "B"), VersionOf(created))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        await Problems.AssertAsync(Assert.Single(responses, r => r.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "VEHICLE_MODIFIED");
    }
}
