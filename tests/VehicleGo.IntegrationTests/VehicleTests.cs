using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using VehicleGo.IntegrationTests.Infrastructure;

namespace VehicleGo.IntegrationTests;

/// <summary>Vehicle endpoints (design §1.4): formats, every §1.5 error code, trash flow and the 30-day purge.</summary>
public sealed class VehicleTests(VehicleGoFactory factory) : ApiTest(factory)
{
    // ---- Create / read / formats ------------------------------------------------------------

    [Fact]
    public async Task CUJ3_新增回201_Location_含inspections和version_格式符合合約()
    {
        using var client = await NewUserAsync();
        var plate = NewPlate();

        var response = await client.PostAsync("/api/vehicles", Inputs.Cuj3Car(plate));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await TestClient.ReadJsonAsync(response);
        var id = IdOf(created);
        Assert.Equal($"/api/vehicles/{id}", response.Headers.Location?.AbsolutePath);
        Assert.NotEqual(0u, VersionOf(created));
        Assert.Equal(plate, created.GetProperty("plate").GetString());
        Assert.Equal("Honda", created.GetProperty("brand").GetString());
        Assert.Equal("Civic", created.GetProperty("model").GetString());
        Assert.Equal("老車", created.GetProperty("nickname").GetString());
        Assert.Equal("白", created.GetProperty("color").GetString());
        Assert.Equal("CAR", created.GetProperty("vehicleType").GetString());
        Assert.Equal("CAR_1201_1800", created.GetProperty("displacementBracket").GetString());
        Assert.Equal("2014-05-01", created.GetProperty("manufactureDate").GetString());
        Assert.Equal("2014-08-20", created.GetProperty("originalIssueDate").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("licenceInspectionDate").ValueKind);
        AssertInspection(Inspection(created, "PERIODIC"), "UPCOMING", "2027-01-20", "2027-03-20", "FORMULA", true, "RTS44-CAR-AGE10UP");
        Assert.False(Inspection(created, "PERIODIC").TryGetProperty("diagnostics", out _));

        var fetched = await client.GetJsonAsync(response.Headers.Location!.ToString());
        Assert.Equal(created.ToString(), fetched.ToString());
    }

    [Fact]
    public async Task CUJ4_修改填入指定檢驗日期_依行照()
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);

        var response = await client.PutAsync($"/api/vehicles/{IdOf(created)}", Inputs.Update(input.With("licenceInspectionDate", "2027-03-10"), VersionOf(created)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await TestClient.ReadJsonAsync(response);
        Assert.NotEqual(VersionOf(created), VersionOf(updated));
        Assert.Equal("2027-03-10", updated.GetProperty("licenceInspectionDate").GetString());
        AssertInspection(Inspection(updated, "PERIODIC"), "UPCOMING", "2027-02-10", "2027-04-10", "LICENCE", false, "RTS44-CAR-LICENCE");
    }

    [Fact]
    public async Task CUJ5_大型重機兩種檢驗都在期間內()
    {
        using var client = await NewUserAsync();

        var created = await CreateAsync(client, Inputs.Cuj5HeavyMc(NewPlate()));

        AssertInspection(Inspection(created, "PERIODIC"), "IN_WINDOW", "2026-10-25", "2026-12-25", "FORMULA", false, "RTS44-HMC-AGE5TO10");
        AssertInspection(Inspection(created, "EXHAUST"), "IN_WINDOW", "2026-10-01", "2026-12-31", "FORMULA", false, "APC40-MC-ANNUAL");
    }

    [Fact]
    public async Task CUJ6_電動機車免檢_排在有日期的車後面()
    {
        using var client = await NewUserAsync();
        var electric = await CreateAsync(client, Inputs.Cuj6ElectricMc("AAA-0001"));
        var car = await CreateAsync(client, Inputs.Car("ZZZ-0001"));

        AssertInspection(Inspection(electric, "EXHAUST"), "EXEMPT", null, null, "FORMULA", false, "APC40-EMC-EXEMPT");
        Assert.Equal(JsonValueKind.Null, electric.GetProperty("displacementBracket").ValueKind);
        var list = (await client.GetJsonAsync("/api/vehicles")).EnumerateArray().Select(IdOf).ToList();
        Assert.Equal([IdOf(car), IdOf(electric)], list);
    }

    [Fact]
    public async Task 列表依最後期限或出廠日期排序()
    {
        using var client = await NewUserAsync();
        var old = await CreateAsync(client, Inputs.Cuj3Car("BBB-0001"));        // deadline 2027-03-20, 2014
        var heavy = await CreateAsync(client, Inputs.Cuj5HeavyMc("CCC-0001"));  // deadline 2026-12-25, 2019-09
        var car = await CreateAsync(client, Inputs.Car("AAA-0001"));            // deadline 2027-08-15, 2019-06

        var byDeadline = (await client.GetJsonAsync("/api/vehicles?sort=deadline")).EnumerateArray().Select(IdOf);
        var byDefault = (await client.GetJsonAsync("/api/vehicles")).EnumerateArray().Select(IdOf);
        var byManufacture = (await client.GetJsonAsync("/api/vehicles?sort=manufactureDate")).EnumerateArray().Select(IdOf);

        Assert.Equal([IdOf(heavy), IdOf(old), IdOf(car)], byDeadline);
        Assert.Equal(byDeadline, byDefault);
        Assert.Equal([IdOf(old), IdOf(car), IdOf(heavy)], byManufacture);
    }

    [Theory]
    [InlineData("expiry")]
    [InlineData("1")]
    [InlineData("Deadline")]
    public async Task 不合法的sort回VALIDATION_FAILED(string sort)
    {
        using var client = await NewUserAsync();

        await Problems.AssertValidationAsync(await client.GetAsync($"/api/vehicles?sort={sort}"), "sort");
    }

    [Fact]
    public async Task 級距清單()
    {
        using var client = await NewUserAsync();

        var brackets = (await client.GetJsonAsync("/api/reference/displacement-brackets")).EnumerateArray().ToList();

        Assert.Equal(16, brackets.Count);
        var mc = Assert.Single(brackets, b => b.GetProperty("code").GetString() == "MC_LE_50");
        Assert.False(string.IsNullOrEmpty(mc.GetProperty("label").GetString()));
        Assert.Equal(["FUEL_MC"], mc.GetProperty("vehicleTypes").EnumerateArray().Select(t => t.GetString()));
    }

    // ---- VALIDATION_FAILED ------------------------------------------------------------------

    [Theory]
    [InlineData("manufactureDate")]
    [InlineData("originalIssueDate")]
    [InlineData("vehicleType")]
    [InlineData("plate")]
    [InlineData("brand")]
    [InlineData("model")]
    public async Task 少了必填欄位回VALIDATION_FAILED_errors用JSON欄位名稱(string field)
    {
        using var client = await NewUserAsync();

        await Problems.AssertValidationAsync(await client.PostAsync("/api/vehicles", Inputs.Car(NewPlate()).Without(field)), field);
    }

    [Fact]
    public async Task 多個欄位缺漏時errors列出所有欄位()
    {
        using var client = await NewUserAsync();

        var response = await client.PostAsync("/api/vehicles", new JsonObject { ["brand"] = "Toyota" });

        await Problems.AssertValidationAsync(response, "plate", "model", "vehicleType", "manufactureDate", "originalIssueDate");
    }

    [Theory]
    [InlineData("manufactureDate", "\"2019/06/01\"")]
    [InlineData("manufactureDate", "\"2019-02-30\"")]
    [InlineData("vehicleType", "\"TRUCK\"")]
    [InlineData("vehicleType", "\"Car\"")]
    [InlineData("vehicleType", "0")]
    [InlineData("displacementBracket", "\"CAR_1201-1800\"")]
    [InlineData("displacementBracket", "3")]
    public async Task 格式錯誤或不認得的代碼回VALIDATION_FAILED(string field, string rawValue)
    {
        using var client = await NewUserAsync();
        var json = Inputs.Car(NewPlate()).Without(field).ToJsonString().TrimEnd('}') + $",\"{field}\":{rawValue}}}";

        var response = await client.SendRawAsync(HttpMethod.Post, "/api/vehicles", json);

        await Problems.AssertValidationAsync(response, field);
    }

    [Fact]
    public async Task 不是JSON時回VALIDATION_FAILED()
    {
        using var client = await NewUserAsync();

        var response = await client.SendRawAsync(HttpMethod.Post, "/api/vehicles", "{not json");

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Theory]
    [InlineData("plate", "ABCDEFGHIJKLMNOPQ")]
    [InlineData("brand", "")]
    [InlineData("model", "   ")]
    [InlineData("nickname", "123456789012345678901234567890123456789012345678901")]
    public async Task 長度不合法回VALIDATION_FAILED(string field, string value)
    {
        using var client = await NewUserAsync();

        await Problems.AssertValidationAsync(await client.PostAsync("/api/vehicles", Inputs.Car(NewPlate()).With(field, value)), field);
    }

    [Fact]
    public async Task 正規化後車牌是空的回VALIDATION_FAILED()
    {
        using var client = await NewUserAsync();

        await Problems.AssertValidationAsync(await client.PostAsync("/api/vehicles", Inputs.Car("- -")), "plate");
    }

    [Theory]
    [InlineData("\"XYZ\"")]
    [InlineData("\"roc\"")]
    [InlineData("1")]
    [InlineData("null")]
    public async Task 不合法的曆法值回VALIDATION_FAILED(string rawValue)
    {
        using var client = await NewUserAsync();

        var response = await client.SendRawAsync(HttpMethod.Patch, "/api/me/preferences", $"{{\"calendar\":{rawValue}}}");

        await Problems.AssertValidationAsync(response, "calendar");
        Assert.Equal("ROC", (await client.GetJsonAsync("/api/me")).GetProperty("calendar").GetString());
    }

    [Fact]
    public async Task 曆法沒帶時回VALIDATION_FAILED()
    {
        using var client = await NewUserAsync();

        await Problems.AssertValidationAsync(await client.PatchAsync("/api/me/preferences", new JsonObject()), "calendar");
    }

    // ---- Domain validation codes ---------------------------------------------------------------

    public static TheoryData<string, JsonObject> DomainErrors() => new()
    {
        { "DATE_IN_FUTURE", Inputs.Car("P").With("manufactureDate", "2026-11-20").With("originalIssueDate", "2026-11-20") },
        { "DATE_IN_FUTURE", Inputs.Car("P").With("originalIssueDate", "2026-11-20") },
        { "ISSUE_BEFORE_MANUFACTURE", Inputs.Car("P").With("originalIssueDate", "2019-05-31") },
        { "BRACKET_TYPE_MISMATCH", Inputs.Car("P").With("displacementBracket", "MC_LE_50") },
        { "BRACKET_TYPE_MISMATCH", Inputs.Car("P").Without("displacementBracket") },
        { "BRACKET_TYPE_MISMATCH", Inputs.Cuj6ElectricMc("P").With("displacementBracket", "MC_LE_50") },
        { "LICENCE_DATE_NOT_ALLOWED", Inputs.FuelMc("P").With("licenceInspectionDate", "2027-04-02") },
        { "LICENCE_DATE_NOT_ALLOWED", Inputs.Cuj6ElectricMc("P").With("licenceInspectionDate", "2027-03-15") },
        { "LICENCE_DATE_BEFORE_ISSUE", Inputs.Car("P").With("licenceInspectionDate", "2019-07-14") },
        { "LICENCE_DATE_TOO_FAR", Inputs.Car("P").With("licenceInspectionDate", "2029-01-01") },
    };

    [Theory]
    [MemberData(nameof(DomainErrors))]
    public async Task Domain驗證錯誤回400和代碼_不建立車輛(string code, JsonObject input)
    {
        using var client = await NewUserAsync();

        await Problems.AssertAsync(await client.PostAsync("/api/vehicles", input.With("plate", NewPlate())), HttpStatusCode.BadRequest, code);
        Assert.Empty((await client.GetJsonAsync("/api/vehicles")).EnumerateArray());
    }

    [Fact]
    public async Task 指定檢驗日期最晚到今年加2年的12月31日()
    {
        using var client = await NewUserAsync();

        await CreateAsync(client, Inputs.Car(NewPlate()).With("licenceInspectionDate", "2028-12-31"));
    }

    [Fact]
    public async Task 修改時也做Domain驗證()
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);

        var response = await client.PutAsync($"/api/vehicles/{IdOf(created)}", Inputs.Update(input.With("originalIssueDate", "2019-01-01"), VersionOf(created)));

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, "ISSUE_BEFORE_MANUFACTURE");
    }

    // ---- Not found / plates / trash ------------------------------------------------------------

    [Fact]
    public async Task 不存在的車回VEHICLE_NOT_FOUND()
    {
        using var client = await NewUserAsync();
        var id = Guid.NewGuid();

        await Problems.AssertAsync(await client.GetAsync($"/api/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await Problems.AssertAsync(await client.PutAsync($"/api/vehicles/{id}", Inputs.Update(Inputs.Car(NewPlate()), 1)), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await Problems.AssertAsync(await client.PostAsync($"/api/vehicles/{id}/trash"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await Problems.AssertAsync(await client.PostAsync($"/api/vehicles/{id}/restore"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await Problems.AssertAsync(await client.DeleteAsync($"/api/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
    }

    [Fact]
    public async Task 車牌和使用中的車重複回PLATE_EXISTS_ACTIVE()
    {
        using var client = await NewUserAsync();
        await CreateAsync(client, Inputs.Car("ABC-1234"));

        await Problems.AssertAsync(await client.PostAsync("/api/vehicles", Inputs.Car("ａｂｃ 1234")), HttpStatusCode.Conflict, "PLATE_EXISTS_ACTIVE");
    }

    [Fact]
    public async Task 修改成別台車的車牌回PLATE_EXISTS_ACTIVE()
    {
        using var client = await NewUserAsync();
        await CreateAsync(client, Inputs.Car("ABC-1234"));
        var other = Inputs.Car("XYZ-0001");
        var created = await CreateAsync(client, other);

        var response = await client.PutAsync($"/api/vehicles/{IdOf(created)}", Inputs.Update(other.With("plate", "abc1234"), VersionOf(created)));

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, "PLATE_EXISTS_ACTIVE");
    }

    [Fact]
    public async Task CUJ7_誤刪後新增同車牌回PLATE_EXISTS_IN_TRASH附trashedVehicleId_還原後回到列表()
    {
        using var client = await NewUserAsync();
        var created = await CreateAsync(client, Inputs.Car("ABC-1234"));
        await TrashAsync(client, IdOf(created));

        var conflict = await Problems.AssertAsync(await client.PostAsync("/api/vehicles", Inputs.Car("abc1234")), HttpStatusCode.Conflict, "PLATE_EXISTS_IN_TRASH");
        Assert.Equal(IdOf(created), conflict.GetProperty("trashedVehicleId").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/vehicles/{IdOf(created)}/restore")).StatusCode);
        Assert.Equal([IdOf(created)], (await client.GetJsonAsync("/api/vehicles")).EnumerateArray().Select(IdOf));
        Assert.Empty((await client.GetJsonAsync("/api/trash")).EnumerateArray());
    }

    [Fact]
    public async Task 修改成垃圾桶裡的車牌回PLATE_EXISTS_IN_TRASH()
    {
        using var client = await NewUserAsync();
        var trashed = await CreateAsync(client, Inputs.Car("ABC-1234"));
        await TrashAsync(client, IdOf(trashed));
        var input = Inputs.Car("XYZ-0001");
        var created = await CreateAsync(client, input);

        var response = await client.PutAsync($"/api/vehicles/{IdOf(created)}", Inputs.Update(input.With("plate", "ABC1234"), VersionOf(created)));

        var problem = await Problems.AssertAsync(response, HttpStatusCode.Conflict, "PLATE_EXISTS_IN_TRASH");
        Assert.Equal(IdOf(trashed), problem.GetProperty("trashedVehicleId").GetGuid());
    }

    [Fact]
    public async Task 垃圾桶流程_列表_詳細_還原_永久刪除()
    {
        using var client = await NewUserAsync();
        var plate = NewPlate();
        var created = await CreateAsync(client, Inputs.Cuj5HeavyMc(plate).With("nickname", "紅牌"));
        var id = IdOf(created);

        await TrashAsync(client, id);

        Assert.Empty((await client.GetJsonAsync("/api/vehicles")).EnumerateArray());
        await Problems.AssertAsync(await client.GetAsync($"/api/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        var item = Assert.Single((await client.GetJsonAsync("/api/trash")).EnumerateArray());
        Assert.Equal(id, IdOf(item));
        Assert.Equal(plate, item.GetProperty("plate").GetString());
        Assert.Equal("Yamaha", item.GetProperty("brand").GetString());
        Assert.Equal("MT-09", item.GetProperty("model").GetString());
        Assert.Equal("紅牌", item.GetProperty("nickname").GetString());
        Assert.Equal("HEAVY_MC", item.GetProperty("vehicleType").GetString());
        Assert.False(item.TryGetProperty("inspections", out _));
        var deletedAt = item.GetProperty("deletedAt").GetDateTimeOffset();
        Assert.Equal(VehicleGoFactory.Now, deletedAt);
        Assert.Equal(TimeSpan.Zero, deletedAt.Offset);
        Assert.Equal(deletedAt.AddDays(30), item.GetProperty("purgeAt").GetDateTimeOffset());
        Assert.EndsWith("+00:00", item.GetProperty("deletedAt").GetString(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/vehicles/{id}")).StatusCode);
        Assert.Empty((await client.GetJsonAsync("/api/trash")).EnumerateArray());
        Assert.Equal(0, await Db.VehicleRowsAsync(id));
    }

    [Fact]
    public async Task 已在垃圾桶的車_再丟或修改回VEHICLE_IN_TRASH()
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);
        await TrashAsync(client, IdOf(created));

        await Problems.AssertAsync(await client.PostAsync($"/api/vehicles/{IdOf(created)}/trash"), HttpStatusCode.Conflict, "VEHICLE_IN_TRASH");
        await Problems.AssertAsync(
            await client.PutAsync($"/api/vehicles/{IdOf(created)}", Inputs.Update(input, VersionOf(created))), HttpStatusCode.Conflict, "VEHICLE_IN_TRASH");
    }

    [Fact]
    public async Task 不在垃圾桶的車_還原或永久刪除回VEHICLE_NOT_IN_TRASH()
    {
        using var client = await NewUserAsync();
        var created = await CreateAsync(client, Inputs.Car(NewPlate()));

        await Problems.AssertAsync(await client.PostAsync($"/api/vehicles/{IdOf(created)}/restore"), HttpStatusCode.Conflict, "VEHICLE_NOT_IN_TRASH");
        await Problems.AssertAsync(await client.DeleteAsync($"/api/vehicles/{IdOf(created)}"), HttpStatusCode.Conflict, "VEHICLE_NOT_IN_TRASH");
        Assert.Equal(1, await Db.VehicleRowsAsync(IdOf(created)));
    }

    // ---- 30-day purge -------------------------------------------------------------------------

    private async Task<Guid> TrashedLongAgoAsync(TestClient client, string plate, int days = 31)
    {
        var created = await CreateAsync(client, Inputs.Car(plate));
        await TrashAsync(client, IdOf(created));
        await Db.SetDeletedAtAsync(IdOf(created), Factory.Time.GetUtcNow().AddDays(-days));
        return IdOf(created);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(30)]
    public async Task 垃圾桶滿30天_其他查詢和操作都看不到(int days)
    {
        using var client = await NewUserAsync();
        var id = await TrashedLongAgoAsync(client, NewPlate(), days);

        Assert.Empty((await client.GetJsonAsync("/api/trash")).EnumerateArray());
        await Problems.AssertAsync(await client.GetAsync($"/api/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await Problems.AssertAsync(await client.PostAsync($"/api/vehicles/{id}/restore"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await Problems.AssertAsync(await client.DeleteAsync($"/api/vehicles/{id}"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        await Problems.AssertAsync(await client.PostAsync($"/api/vehicles/{id}/trash"), HttpStatusCode.NotFound, "VEHICLE_NOT_FOUND");
        Assert.Equal(1, await Db.VehicleRowsAsync(id)); // reads and these operations do not purge
    }

    [Fact]
    public async Task 垃圾桶未滿30天仍在垃圾桶()
    {
        using var client = await NewUserAsync();
        var id = await TrashedLongAgoAsync(client, NewPlate(), days: 29);

        Assert.Equal([id], (await client.GetJsonAsync("/api/trash")).EnumerateArray().Select(IdOf));
    }

    [Fact]
    public async Task 成功新增後_滿30天的車已被永久刪除()
    {
        using var client = await NewUserAsync();
        var expired = await TrashedLongAgoAsync(client, NewPlate());

        await CreateAsync(client, Inputs.Car(NewPlate()));

        Assert.Equal(0, await Db.VehicleRowsAsync(expired));
    }

    [Fact]
    public async Task 成功修改後_滿30天的車已被永久刪除()
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var active = await CreateAsync(client, input);
        var expired = await TrashedLongAgoAsync(client, NewPlate());

        var response = await client.PutAsync($"/api/vehicles/{IdOf(active)}", Inputs.Update(input.With("nickname", "改"), VersionOf(active)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await Db.VehicleRowsAsync(expired));
    }

    [Fact]
    public async Task 新增失敗時交易rollback_清除也一起復原()
    {
        using var client = await NewUserAsync();
        var activePlate = NewPlate();
        await CreateAsync(client, Inputs.Car(activePlate));
        var expired = await TrashedLongAgoAsync(client, NewPlate());

        await Problems.AssertAsync(await client.PostAsync("/api/vehicles", Inputs.Car(activePlate)), HttpStatusCode.Conflict, "PLATE_EXISTS_ACTIVE");

        Assert.Equal(1, await Db.VehicleRowsAsync(expired));
    }

    [Fact]
    public async Task 新增和滿30天的車相同車牌時成功()
    {
        using var client = await NewUserAsync();
        var plate = NewPlate();
        var expired = await TrashedLongAgoAsync(client, plate);

        await CreateAsync(client, Inputs.Car(plate));

        Assert.Equal(0, await Db.VehicleRowsAsync(expired));
    }

    [Fact]
    public async Task PUT把車牌改成和滿30天的車相同時成功()
    {
        using var client = await NewUserAsync();
        var plate = NewPlate();
        var expired = await TrashedLongAgoAsync(client, plate);
        var input = Inputs.Car(NewPlate());
        var active = await CreateAsync(client, input);

        var response = await client.PutAsync($"/api/vehicles/{IdOf(active)}", Inputs.Update(input.With("plate", plate), VersionOf(active)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(plate, (await TestClient.ReadJsonAsync(response)).GetProperty("plate").GetString());
        Assert.Equal(0, await Db.VehicleRowsAsync(expired));
    }
}
