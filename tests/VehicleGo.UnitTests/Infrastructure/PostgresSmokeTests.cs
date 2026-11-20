using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Users;
using VehicleGo.Domain.Vehicles;
using VehicleGo.Infrastructure.DataProtection;
using VehicleGo.Infrastructure.Persistence;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Infrastructure;

/// <summary>
/// Starts PostgreSQL (tag from <c>postgres-image.txt</c>) once and applies both migrations.
/// Needs Docker. Full repository behaviour is covered by the integration tests (step 5).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString => _container!.GetConnectionString();

    public static string ImageTag()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "postgres-image.txt");
            if (File.Exists(file))
            {
                return File.ReadAllText(file).Trim();
            }
        }

        throw new FileNotFoundException("postgres-image.txt not found above the test output directory.");
    }

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(ImageTag()).Build();
        await _container.StartAsync();

        await using var provider = InfrastructureServices.Build(ConnectionString, new FakeCurrentUser());
        await DatabaseMigrator.MigrateAsync(provider, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresSmokeTests(PostgresFixture postgres)
{
    private static readonly DateOnly Manufacture = new(2019, 3, 10);
    private static readonly DateOnly Issue = new(2019, 4, 2);
    private static readonly DateOnly Today = new(2026, 11, 19);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 1, 0, 0, TimeSpan.Zero));

    private ServiceProvider Services(FakeCurrentUser user) => InfrastructureServices.Build(postgres.ConnectionString, user, _time);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static VehicleDetails Car(string plate) =>
        new(plate, "Toyota", "Altis", null, null, VehicleType.Car, DisplacementBracket.Car1201To1800, Manufacture, Issue, null);

    /// <summary>Creates a user through the login upsert and returns a current user for it.</summary>
    private async Task<FakeCurrentUser> NewUserAsync()
    {
        var anonymous = new FakeCurrentUser { UserId = Guid.Empty };
        await using var provider = Services(anonymous);
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var id = await users.UpsertOnLoginAsync($"sub-{Guid.NewGuid():N}", [1, 2, 3], [4, 5, 6], Ct);
        return new FakeCurrentUser { UserId = id };
    }

    private async Task<Vehicle> AddCarAsync(FakeCurrentUser user, string plate)
    {
        await using var provider = Services(user);
        using var scope = provider.CreateScope();
        var vehicle = Vehicle.Create(Guid.CreateVersion7(), user.UserId, Car(plate), Today, _time.GetUtcNow()).Value;
        var result = await scope.ServiceProvider.GetRequiredService<IVehicleRepository>().AddAsync(vehicle, Ct);
        Assert.True(result.IsSuccess);
        return vehicle;
    }

    private async Task<PostgresException> InsertFailsAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
    }

    private static string VehicleInsert(Guid owner, string type = "'CAR'", string bracket = "'CAR_1201_1800'", string licence = "NULL",
        string manufacture = "'2019-03-10'", string issue = "'2019-04-02'", string plate = "'ABC-1234'", string normalized = "'ABC1234'") =>
        $"""
        INSERT INTO vehicles (id, owner_id, plate, normalized_plate, brand, model, vehicle_type, displacement_bracket,
                              manufacture_date, original_issue_date, licence_inspection_date, created_at, updated_at)
        VALUES ('{Guid.NewGuid()}', '{owner}', {plate}, {normalized}, 'Toyota', 'Altis', {type}, {bracket},
                {manufacture}, {issue}, {licence}, now(), now())
        """;

    [Fact]
    public async Task 兩個migration都已套用()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name", connection);
        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Equal(["__ef_dp_migrations_history", "__ef_migrations_history", "data_protection_keys", "users", "vehicles"], tables);
    }

    [Fact]
    public async Task check_constraint接受合法的資料()
    {
        var user = await NewUserAsync();

        var vehicle = await AddCarAsync(user, "ABC-1234");

        Assert.NotEqual(0u, vehicle.Version);
    }

    [Theory]
    [InlineData("ck_vehicles_bracket", "'ELECTRIC_MC'", "'CAR_ELECTRIC'", "NULL")]
    [InlineData("ck_vehicles_bracket", "'CAR'", "NULL", "NULL")]
    [InlineData("ck_vehicles_bracket", "'FUEL_MC'", "'MC_GT_550'", "NULL")]
    [InlineData("ck_vehicles_licence_date", "'FUEL_MC'", "'MC_LE_50'", "'2027-04-02'")]
    [InlineData("ck_vehicles_type", "'TRUCK'", "NULL", "NULL")]
    public async Task check_constraint拒絕不合法的車種組合(string constraint, string type, string bracket, string licence)
    {
        var user = await NewUserAsync();

        var error = await InsertFailsAsync(VehicleInsert(user.UserId, type, bracket, licence));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        if (constraint == "ck_vehicles_type")
        {
            Assert.Contains(error.ConstraintName, new[] { "ck_vehicles_type", "ck_vehicles_bracket" });
        }
        else
        {
            Assert.Equal(constraint, error.ConstraintName);
        }
    }

    [Fact]
    public async Task check_constraint拒絕發照早於出廠()
    {
        var user = await NewUserAsync();

        var error = await InsertFailsAsync(VehicleInsert(user.UserId, manufacture: "'2019-04-02'", issue: "'2019-03-10'"));

        Assert.Equal("ck_vehicles_dates", error.ConstraintName);
    }

    [Theory]
    [InlineData("''", "'X'", "ck_vehicles_plate_length")]
    [InlineData("'ABCDEFGHIJKLMNOPQ'", "'X'", "ck_vehicles_plate_length")]
    [InlineData("'-'", "''", "ck_vehicles_normalized_plate_length")]
    public async Task check_constraint限制車牌長度(string plate, string normalized, string constraint)
    {
        var user = await NewUserAsync();

        var error = await InsertFailsAsync(VehicleInsert(user.UserId, plate: plate, normalized: normalized));

        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task check_constraint拒絕不合法的曆法()
    {
        var user = await NewUserAsync();

        var error = await InsertFailsAsync($"UPDATE users SET calendar_preference = 'Roc' WHERE id = '{user.UserId}'");

        Assert.Equal("ck_users_calendar", error.ConstraintName);
    }

    [Fact]
    public async Task 登入upsert同一個sub只建立一筆_預設ROC()
    {
        await using var provider = Services(new FakeCurrentUser { UserId = Guid.Empty });
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var sub = $"sub-{Guid.NewGuid():N}";

        var first = await users.UpsertOnLoginAsync(sub, [1], [2], Ct);
        var second = await users.UpsertOnLoginAsync(sub, [9], [8], Ct);

        Assert.Equal(first, second);
        var user = await users.GetAsync(first, Ct);
        Assert.NotNull(user);
        Assert.Equal([9], user.EmailEncrypted);
        Assert.Equal(CalendarPreference.Roc, await users.GetCalendarPreferenceAsync(first, Ct));

        user.ChangeCalendarPreference(CalendarPreference.Gregorian, _time.GetUtcNow());
        await users.SaveAsync(user, Ct);
        Assert.Equal(CalendarPreference.Gregorian, await users.GetCalendarPreferenceAsync(first, Ct));
    }

    [Fact]
    public async Task 舊版本號存檔回VEHICLE_MODIFIED()
    {
        var user = await NewUserAsync();
        var added = await AddCarAsync(user, "VER-0001");
        await using var provider = Services(user);
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var cutoff = _time.GetUtcNow().AddDays(-30);

        var vehicle = await repository.GetForOwnerAsync(added.Id, includeTrashed: false, cutoff, Ct);
        Assert.NotNull(vehicle);
        Assert.Equal(added.Version, vehicle.Version);
        vehicle.Update(Car("VER-0001") with { Nickname = "new" }, Today, _time.GetUtcNow());

        var stale = await repository.SaveAsync(vehicle, added.Version + 1000, Ct);

        Assert.Equal(ErrorCode.VehicleModified, stale.Error?.Code);
    }

    [Fact]
    public async Task 版本號一致時存檔成功並更新版本號()
    {
        var user = await NewUserAsync();
        var added = await AddCarAsync(user, "VER-0002");
        await using var provider = Services(user);
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        var vehicle = (await repository.GetForOwnerAsync(added.Id, false, _time.GetUtcNow().AddDays(-30), Ct))!;
        vehicle.Update(Car("VER-0002") with { Nickname = "new" }, Today, _time.GetUtcNow());
        var saved = await repository.SaveAsync(vehicle, added.Version, Ct);

        Assert.True(saved.IsSuccess);
        Assert.NotEqual(added.Version, vehicle.Version);
    }

    [Fact]
    public async Task 同時寫入撞到車牌唯一索引回PLATE_EXISTS_ACTIVE()
    {
        var user = await NewUserAsync();
        await AddCarAsync(user, "DUP-0001");
        await using var provider = Services(user);
        using var scope = provider.CreateScope();
        var duplicate = Vehicle.Create(Guid.CreateVersion7(), user.UserId, Car("dup 0001"), Today, _time.GetUtcNow()).Value;

        var result = await scope.ServiceProvider.GetRequiredService<IVehicleRepository>().AddAsync(duplicate, Ct);

        Assert.Equal(ErrorCode.PlateExistsActive, result.Error?.Code);
    }

    [Fact]
    public async Task 別人的車查不到_相同車牌可以登錄()
    {
        var alice = await NewUserAsync();
        var bob = await NewUserAsync();
        var aliceCar = await AddCarAsync(alice, "ISO-0001");
        await AddCarAsync(bob, "ISO-0001");

        await using var provider = Services(bob);
        using var scope = provider.CreateScope();
        var cutoff = _time.GetUtcNow().AddDays(-30);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IVehicleRepository>().GetForOwnerAsync(aliceCar.Id, true, cutoff, Ct));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IVehicleQueries>().GetActiveAsync(aliceCar.Id, Ct));
        var mine = await scope.ServiceProvider.GetRequiredService<IVehicleQueries>().ListActiveAsync(Ct);
        Assert.All(mine, v => Assert.Equal(bob.UserId, v.OwnerId));
        Assert.Single(mine);
    }

    [Fact]
    public async Task 垃圾桶_計數_清除_只關閉Deleted過濾器()
    {
        var user = await NewUserAsync();
        var other = await NewUserAsync();
        var active = await AddCarAsync(user, "TRS-0001");
        var recent = await AddCarAsync(user, "TRS-0002");
        var expired = await AddCarAsync(user, "TRS-0003");
        var othersExpired = await AddCarAsync(other, "TRS-0003");
        var now = _time.GetUtcNow();
        var cutoff = now.AddDays(-30);
        await SetDeletedAtAsync(recent.Id, now.AddDays(-29));
        await SetDeletedAtAsync(expired.Id, cutoff);
        await SetDeletedAtAsync(othersExpired.Id, now.AddDays(-31));

        await using var provider = Services(user);
        using (var scope = provider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var queries = scope.ServiceProvider.GetRequiredService<IVehicleQueries>();

            Assert.Equal((1, 2), await repository.CountForOwnerAsync(Ct));
            Assert.Equal([recent.Id], (await queries.ListTrashAsync(cutoff, Ct)).Select(v => v.Id));
            Assert.Equal([active.Id], (await queries.ListActiveAsync(Ct)).Select(v => v.Id));
            Assert.Null(await repository.GetForOwnerAsync(expired.Id, true, cutoff, Ct));
            Assert.Null(await repository.GetForOwnerAsync(recent.Id, false, cutoff, Ct));
            Assert.NotNull(await repository.GetForOwnerAsync(recent.Id, true, cutoff, Ct));
            Assert.Null(await repository.FindByNormalizedPlateAsync("TRS0003", cutoff, Ct));
            Assert.Equal(recent.Id, (await repository.FindByNormalizedPlateAsync("TRS0002", cutoff, Ct))?.Id);

            await repository.PurgeExpiredTrashAsync(cutoff, Ct);
            Assert.Equal((1, 1), await repository.CountForOwnerAsync(Ct));
        }

        // The other user's expired vehicle is not purged by this user.
        await using var otherProvider = Services(other);
        using var otherScope = otherProvider.CreateScope();
        Assert.Equal((0, 1), await otherScope.ServiceProvider.GetRequiredService<IVehicleRepository>().CountForOwnerAsync(Ct));
    }

    [Fact]
    public async Task 交易中鎖住使用者_失敗時rollback()
    {
        var user = await NewUserAsync();
        await using var provider = Services(user);
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var result = await unitOfWork.ExecuteInTransactionAsync<int>(async ct =>
        {
            await repository.LockOwnerAsync(ct);
            var vehicle = Vehicle.Create(Guid.CreateVersion7(), user.UserId, Car("TX-0001"), Today, _time.GetUtcNow()).Value;
            Assert.True((await repository.AddAsync(vehicle, ct)).IsSuccess);
            return Result<int>.Failure(ErrorCode.VehicleLimitReached);
        }, Ct);

        Assert.True(result.IsFailure);
        Assert.Equal((0, 0), await repository.CountForOwnerAsync(Ct));

        var committed = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await repository.LockOwnerAsync(ct);
            var vehicle = Vehicle.Create(Guid.CreateVersion7(), user.UserId, Car("TX-0002"), Today, _time.GetUtcNow()).Value;
            await repository.AddAsync(vehicle, ct);
            return Result.Success(1);
        }, Ct);

        Assert.True(committed.IsSuccess);
        Assert.Equal((1, 0), await repository.CountForOwnerAsync(Ct));
    }

    [Fact]
    public async Task 永久刪除()
    {
        var user = await NewUserAsync();
        var added = await AddCarAsync(user, "DEL-0001");
        await using var provider = Services(user);
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var cutoff = _time.GetUtcNow().AddDays(-30);
        var vehicle = (await repository.GetForOwnerAsync(added.Id, true, cutoff, Ct))!;
        vehicle.Trash(_time.GetUtcNow());
        Assert.True((await repository.SaveAsync(vehicle, null, Ct)).IsSuccess);

        Assert.True((await repository.DeleteAsync(vehicle, Ct)).IsSuccess);

        Assert.Equal((0, 0), await repository.CountForOwnerAsync(Ct));
    }

    [Fact]
    public async Task DataProtection金鑰加密後存進data_protection_keys()
    {
        await using var provider = Services(new FakeCurrentUser { UserId = Guid.Empty });
        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("smoke-test");
        Assert.Equal("hello", protector.Unprotect(protector.Protect("hello")));

        using var scope = provider.CreateScope();
        var keys = await scope.ServiceProvider.GetRequiredService<DataProtectionKeyContext>().DataProtectionKeys.ToListAsync(Ct);
        Assert.NotEmpty(keys);
        Assert.All(keys, k =>
        {
            Assert.Contains(nameof(AesGcmXmlDecryptor), k.Xml, StringComparison.Ordinal);
            Assert.DoesNotContain("<masterKey", k.Xml, StringComparison.Ordinal);
        });
    }

    private async Task SetDeletedAtAsync(Guid vehicleId, DateTimeOffset deletedAt)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("UPDATE vehicles SET deleted_at = @d WHERE id = @id", connection);
        command.Parameters.AddWithValue("d", deletedAt);
        command.Parameters.AddWithValue("id", vehicleId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
    }
}
