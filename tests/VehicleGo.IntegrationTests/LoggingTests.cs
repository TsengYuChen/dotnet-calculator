using System.Net;
using VehicleGo.Domain.Inspections;
using VehicleGo.Domain.Inspections.Rules;
using VehicleGo.Domain.Vehicles;
using VehicleGo.IntegrationTests.Infrastructure;

namespace VehicleGo.IntegrationTests;

/// <summary>Structured logs: LICENCE_DATE_MISMATCH / INSPECTION_UNCOMPUTABLE only on create / update, never the plate or e-mail.</summary>
public sealed class LoggingTests(VehicleGoFactory factory) : ApiTest(factory)
{
    private IReadOnlyList<LogEntry> EventsFor(string eventName, Guid vehicleId) =>
        Factory.Logs.Event(eventName).Where(e => Equals(e.Fields.GetValueOrDefault("VehicleId"), vehicleId)).ToList();

    private void AssertNoLogContains(string text) =>
        Assert.DoesNotContain(Factory.Logs.Entries, e =>
            e.Message.Contains(text, StringComparison.OrdinalIgnoreCase)
            || e.Fields.Values.Any(v => v?.ToString()?.Contains(text, StringComparison.OrdinalIgnoreCase) == true));

    [Fact]
    public async Task CUJ4_新增時指定檢驗日期不一致_寫LICENCE_DATE_MISMATCH_不含車牌_讀取不寫()
    {
        using var client = await NewUserAsync();
        var plate = $"MIS-{Guid.NewGuid():N}"[..10];

        var created = await CreateAsync(client, Inputs.Car(plate).With("licenceInspectionDate", "2027-03-10"));
        var id = IdOf(created);
        await client.GetAsync($"/api/vehicles/{id}");
        await client.GetAsync("/api/vehicles");

        var entry = Assert.Single(EventsFor("LICENCE_DATE_MISMATCH", id));
        Assert.Equal("CAR", entry.Fields["VehicleType"]);
        Assert.Equal("2019-06-01", entry.Fields["ManufactureDate"]);
        Assert.Equal("2019-07-15", entry.Fields["OriginalIssueDate"]);
        Assert.Equal("2027-03-10", entry.Fields["LicenceInspectionDate"]);
        Assert.False(string.IsNullOrEmpty(entry.Fields["FormulaDate"]?.ToString()));
        AssertNoLogContains(plate);
    }

    [Fact]
    public async Task 修改時指定檢驗日期不一致_寫LICENCE_DATE_MISMATCH()
    {
        using var client = await NewUserAsync();
        var input = Inputs.Car(NewPlate());
        var created = await CreateAsync(client, input);
        Assert.Empty(EventsFor("LICENCE_DATE_MISMATCH", IdOf(created)));

        var response = await client.PutAsync($"/api/vehicles/{IdOf(created)}", Inputs.Update(input.With("licenceInspectionDate", "2027-03-10"), VersionOf(created)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(EventsFor("LICENCE_DATE_MISMATCH", IdOf(created)));
    }

    [Fact]
    public async Task CUJ4c_一致時不寫log()
    {
        using var client = await NewUserAsync();

        var created = await CreateAsync(client, Inputs.Cuj3Car(NewPlate()).With("licenceInspectionDate", "2027-08-20"));

        AssertInspection(Inspection(created, "PERIODIC"), "UPCOMING", "2027-01-20", "2027-03-20", "LICENCE", true, "RTS44-CAR-LICENCE");
        Assert.Empty(EventsFor("LICENCE_DATE_MISMATCH", IdOf(created)));
    }

    [Fact]
    public async Task 規則表缺規則時_新增寫INSPECTION_UNCOMPUTABLE_回應為UNCOMPUTABLE_讀取不寫()
    {
        using var client = await NewUserAsync();
        var plate = $"UNC-{Guid.NewGuid():N}"[..10];
        Factory.Rules.Rules = RuleTable.Current().Without(VehicleType.HeavyMc, InspectionKind.Exhaust);
        try
        {
            var created = await CreateAsync(client, Inputs.Cuj5HeavyMc(plate));
            var id = IdOf(created);
            var fetched = await client.GetJsonAsync($"/api/vehicles/{id}");
            await client.GetAsync("/api/vehicles");

            AssertInspection(Inspection(fetched, "EXHAUST"), "UNCOMPUTABLE", null, null, "FORMULA", false, null);
            AssertInspection(Inspection(fetched, "PERIODIC"), "IN_WINDOW", "2026-10-25", "2026-12-25", "FORMULA", false, "RTS44-HMC-AGE5TO10");
            var entry = Assert.Single(EventsFor("INSPECTION_UNCOMPUTABLE", id));
            Assert.Equal("EXHAUST", entry.Fields["Kind"]);
            Assert.Equal("NO_RULE", entry.Fields["Reason"]);
            AssertNoLogContains(plate);
        }
        finally
        {
            Factory.Rules.Reset();
        }
    }

    [Fact]
    public async Task 登入被拒絕時寫LOGIN_REJECTED_只含Email雜湊前8碼()
    {
        var email = TestClient.NewEmail("rejected-log");
        using var client = TestClient.Create(Factory, email, $"google-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.TryLoginAsync()).StatusCode);

        var entry = Assert.Single(Factory.Logs.Event("LOGIN_REJECTED"), e => e.Fields.GetValueOrDefault("Subject")?.ToString() == client.Subject);
        Assert.Matches("^[0-9a-f]{8}$", entry.Fields["EmailHash"]?.ToString());
        Assert.Equal("NOT_ALLOWED", entry.Fields["Reason"]);
        AssertNoLogContains(email);
    }

    [Fact]
    public async Task 登入成功和移出名單的log不含Email()
    {
        using var client = await NewUserAsync();
        await CreateAsync(client, Inputs.Car(NewPlate()));
        Factory.Allowlist.Remove(client.Email);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);

        Assert.Contains(Factory.Logs.Event("ALLOWLIST_REVOKED"), e => e.Fields.GetValueOrDefault("Subject")?.ToString() == client.Subject);
        AssertNoLogContains(client.Email);
    }
}
