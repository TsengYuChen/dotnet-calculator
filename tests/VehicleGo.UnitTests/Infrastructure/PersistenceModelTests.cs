using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VehicleGo.Domain.Users;
using VehicleGo.Domain.Vehicles;
using VehicleGo.Infrastructure.DataProtection;
using VehicleGo.Infrastructure.Persistence;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Infrastructure;

/// <summary>
/// Design §1.6: the EF model's table, column, index and constraint names, checked without a database
/// (design-time model of the design-time factories).
/// </summary>
public class PersistenceModelTests
{
    private static IModel AppModel() =>
        new AppDbContextFactory().CreateDbContext([]).GetService<IDesignTimeModel>().Model;

    private static IEntityType Entity<T>() => AppModel().FindEntityType(typeof(T))!;

    private static AppDbContext ContextFor(Guid userId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContext.Configure(options, AppDbContextFactory.DesignTimeConnectionString);
        return new AppDbContext(options.Options, new FakeCurrentUser { UserId = userId });
    }

    [Fact]
    public void 資料表名稱()
    {
        Assert.Equal("users", Entity<User>().GetTableName());
        Assert.Equal("vehicles", Entity<Vehicle>().GetTableName());
        Assert.Null(Entity<User>().GetSchema());
        Assert.Equal(2, AppModel().GetEntityTypes().Count());
    }

    [Theory]
    [InlineData(nameof(User.Id), "id", "uuid", false)]
    [InlineData(nameof(User.GoogleSub), "google_sub", "text", false)]
    [InlineData(nameof(User.EmailEncrypted), "email_encrypted", "bytea", false)]
    [InlineData(nameof(User.EmailLookupHash), "email_lookup_hash", "bytea", false)]
    [InlineData(nameof(User.CalendarPreference), "calendar_preference", "text", false)]
    [InlineData(nameof(User.CreatedAt), "created_at", "timestamp with time zone", false)]
    [InlineData(nameof(User.UpdatedAt), "updated_at", "timestamp with time zone", false)]
    public void users的欄位(string property, string column, string type, bool nullable) =>
        AssertColumn(Entity<User>(), property, column, type, nullable);

    [Theory]
    [InlineData(nameof(Vehicle.Id), "id", "uuid", false)]
    [InlineData(nameof(Vehicle.OwnerId), "owner_id", "uuid", false)]
    [InlineData(nameof(Vehicle.Plate), "plate", "text", false)]
    [InlineData(nameof(Vehicle.NormalizedPlate), "normalized_plate", "text", false)]
    [InlineData(nameof(Vehicle.Brand), "brand", "text", false)]
    [InlineData(nameof(Vehicle.Model), "model", "text", false)]
    [InlineData(nameof(Vehicle.Nickname), "nickname", "text", true)]
    [InlineData(nameof(Vehicle.Color), "color", "text", true)]
    [InlineData(nameof(Vehicle.VehicleType), "vehicle_type", "text", false)]
    [InlineData(nameof(Vehicle.DisplacementBracket), "displacement_bracket", "text", true)]
    [InlineData(nameof(Vehicle.ManufactureDate), "manufacture_date", "date", false)]
    [InlineData(nameof(Vehicle.OriginalIssueDate), "original_issue_date", "date", false)]
    [InlineData(nameof(Vehicle.LicenceInspectionDate), "licence_inspection_date", "date", true)]
    [InlineData(nameof(Vehicle.DeletedAt), "deleted_at", "timestamp with time zone", true)]
    [InlineData(nameof(Vehicle.CreatedAt), "created_at", "timestamp with time zone", false)]
    [InlineData(nameof(Vehicle.UpdatedAt), "updated_at", "timestamp with time zone", false)]
    [InlineData(nameof(Vehicle.Version), "xmin", "xid", false)]
    public void vehicles的欄位(string property, string column, string type, bool nullable) =>
        AssertColumn(Entity<Vehicle>(), property, column, type, nullable);

    private static void AssertColumn(IEntityType entity, string propertyName, string column, string type, bool nullable)
    {
        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(column, property.GetColumnName());
        Assert.Equal(type, property.GetColumnType());
        Assert.Equal(nullable, property.IsColumnNullable());
    }

    [Fact]
    public void 只對應設計列出的欄位()
    {
        Assert.Equal(7, Entity<User>().GetProperties().Count());
        Assert.Equal(17, Entity<Vehicle>().GetProperties().Count());
    }

    [Fact]
    public void 代碼enum欄位用共用的ValueConverter()
    {
        Assert.IsType<CodeValueConverter<VehicleType>>(Entity<Vehicle>().FindProperty(nameof(Vehicle.VehicleType))!.GetValueConverter());
        Assert.IsType<CodeValueConverter<DisplacementBracket>>(Entity<Vehicle>().FindProperty(nameof(Vehicle.DisplacementBracket))!.GetValueConverter());
        Assert.IsType<CodeValueConverter<CalendarPreference>>(Entity<User>().FindProperty(nameof(User.CalendarPreference))!.GetValueConverter());
    }

    [Fact]
    public void calendar_preference預設ROC()
    {
        var property = Entity<User>().FindProperty(nameof(User.CalendarPreference))!;

        Assert.NotNull(property.FindAnnotation(RelationalAnnotationNames.DefaultValue));
        Assert.Equal(CalendarPreference.Roc, property.GetDefaultValue());
    }

    [Fact]
    public void Version是xmin版本號()
    {
        var property = Entity<Vehicle>().FindProperty(nameof(Vehicle.Version))!;

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public void 主鍵名稱()
    {
        Assert.Equal("pk_users", Entity<User>().FindPrimaryKey()!.GetName());
        Assert.Equal("pk_vehicles", Entity<Vehicle>().FindPrimaryKey()!.GetName());
    }

    [Fact]
    public void users的索引()
    {
        var indexes = Entity<User>().GetIndexes().ToDictionary(i => i.GetDatabaseName()!);

        Assert.Equal(["ix_users_email_lookup_hash", "ux_users_google_sub"], indexes.Keys.Order());
        Assert.True(indexes["ux_users_google_sub"].IsUnique);
        Assert.Equal(["google_sub"], Columns(indexes["ux_users_google_sub"]));
        Assert.False(indexes["ix_users_email_lookup_hash"].IsUnique);
        Assert.Equal(["email_lookup_hash"], Columns(indexes["ix_users_email_lookup_hash"]));
    }

    [Fact]
    public void vehicles的索引()
    {
        var indexes = Entity<Vehicle>().GetIndexes().ToDictionary(i => i.GetDatabaseName()!);

        Assert.Equal(["ix_vehicles_owner_deleted", "ux_vehicles_owner_plate"], indexes.Keys.Order());
        Assert.True(indexes["ux_vehicles_owner_plate"].IsUnique);
        Assert.Equal(["owner_id", "normalized_plate"], Columns(indexes["ux_vehicles_owner_plate"]));
        Assert.False(indexes["ix_vehicles_owner_deleted"].IsUnique);
        Assert.Equal(["owner_id", "deleted_at"], Columns(indexes["ix_vehicles_owner_deleted"]));
    }

    private static string[] Columns(IIndex index) => index.Properties.Select(p => p.GetColumnName()).ToArray();

    [Fact]
    public void vehicles的外鍵()
    {
        var foreignKey = Assert.Single(Entity<Vehicle>().GetForeignKeys());

        Assert.Equal("fk_vehicles_owner", foreignKey.GetConstraintName());
        Assert.Equal(typeof(User), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(["owner_id"], foreignKey.Properties.Select(p => p.GetColumnName()));
        Assert.True(foreignKey.IsRequired);
    }

    [Fact]
    public void check_constraint名稱()
    {
        Assert.Equal(["ck_users_calendar"], Entity<User>().GetCheckConstraints().Select(c => c.Name));
        Assert.Equal(
            [
                "ck_vehicles_bracket", "ck_vehicles_brand_length", "ck_vehicles_color_length", "ck_vehicles_dates",
                "ck_vehicles_licence_date", "ck_vehicles_model_length", "ck_vehicles_nickname_length",
                "ck_vehicles_normalized_plate_length", "ck_vehicles_plate_length", "ck_vehicles_type",
            ],
            Entity<Vehicle>().GetCheckConstraints().Select(c => c.Name!).Order());
    }

    [Fact]
    public void check_constraint使用1_1節的代碼()
    {
        string Sql(IEntityType entity, string name) => entity.GetCheckConstraints().Single(c => c.Name == name).Sql;

        Assert.Equal("calendar_preference IN ('ROC', 'GREGORIAN')", Sql(Entity<User>(), "ck_users_calendar"));
        Assert.Equal("vehicle_type IN ('CAR', 'FUEL_MC', 'HEAVY_MC', 'ELECTRIC_MC')", Sql(Entity<Vehicle>(), "ck_vehicles_type"));
        Assert.Equal("original_issue_date >= manufacture_date", Sql(Entity<Vehicle>(), "ck_vehicles_dates"));
        Assert.Equal(
            "licence_inspection_date IS NULL OR vehicle_type IN ('CAR', 'HEAVY_MC')",
            Sql(Entity<Vehicle>(), "ck_vehicles_licence_date"));

        var bracket = Sql(Entity<Vehicle>(), "ck_vehicles_bracket");
        Assert.Contains("(vehicle_type = 'ELECTRIC_MC' AND displacement_bracket IS NULL)", bracket, StringComparison.Ordinal);
        Assert.Contains("(vehicle_type = 'FUEL_MC' AND displacement_bracket IS NOT NULL AND displacement_bracket IN ('MC_LE_50', 'MC_51_250'))", bracket, StringComparison.Ordinal);
        Assert.Contains("(vehicle_type = 'HEAVY_MC' AND displacement_bracket IS NOT NULL AND displacement_bracket IN ('MC_251_550', 'MC_GT_550'))", bracket, StringComparison.Ordinal);
        Assert.Contains("(vehicle_type = 'CAR' AND displacement_bracket IS NOT NULL AND displacement_bracket IN ('CAR_LE_500', ", bracket, StringComparison.Ordinal);
        Assert.Contains("'CAR_GT_7800', 'CAR_ELECTRIC'))", bracket, StringComparison.Ordinal);
    }

    [Fact]
    public void vehicles有Owner和Deleted兩個具名查詢過濾器()
    {
        var filters = Entity<Vehicle>().GetDeclaredQueryFilters().Select(f => f.Key).Order();

        Assert.Equal(["Deleted", "Owner"], filters);
        Assert.Equal("Owner", AppDbContext.OwnerFilter);
        Assert.Equal("Deleted", AppDbContext.DeletedFilter);
        Assert.Empty(Entity<User>().GetDeclaredQueryFilters());
    }

    [Fact]
    public void Owner過濾器用目前使用者_Deleted過濾器排除垃圾桶()
    {
        var userId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        using var context = ContextFor(userId);

        var sql = context.Vehicles.ToQueryString();

        Assert.Contains(userId.ToString(), sql, StringComparison.Ordinal);
        Assert.Contains("v.owner_id = @", sql, StringComparison.Ordinal);
        Assert.Contains("v.deleted_at IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void 只關閉Deleted過濾器時仍限定擁有者()
    {
        var userId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        using var context = ContextFor(userId);

        var sql = context.Vehicles.IgnoreQueryFilters([AppDbContext.DeletedFilter]).ToQueryString();

        Assert.Contains(userId.ToString(), sql, StringComparison.Ordinal);
        Assert.DoesNotContain("deleted_at IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void 沒有目前使用者時Owner過濾器用Guid_Empty()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);

        Assert.Equal(Guid.Empty, context.CurrentUserId);
        Assert.Contains(Guid.Empty.ToString(), context.Vehicles.ToQueryString(), StringComparison.Ordinal);
    }

    [Fact]
    public void migration歷史表名稱()
    {
        Assert.Equal("__ef_migrations_history", HistoryTable(new AppDbContextFactory().CreateDbContext([])));
        Assert.Equal("__ef_dp_migrations_history", HistoryTable(new DataProtectionKeyContextFactory().CreateDbContext([])));
    }

    private static string? HistoryTable(DbContext context) =>
        RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>()).MigrationsHistoryTableName;

    [Fact]
    public void data_protection_keys的名稱()
    {
        using var context = new DataProtectionKeyContextFactory().CreateDbContext([]);
        var entity = Assert.Single(context.GetService<IDesignTimeModel>().Model.GetEntityTypes());

        Assert.Equal("data_protection_keys", entity.GetTableName());
        Assert.Equal("pk_data_protection_keys", entity.FindPrimaryKey()!.GetName());
        Assert.Equal(["friendly_name", "id", "xml"], entity.GetProperties().Select(p => p.GetColumnName()).Order());
    }

    [Fact]
    public void 兩個DbContext的migration和模型一致()
    {
        using var app = new AppDbContextFactory().CreateDbContext([]);
        using var dataProtection = new DataProtectionKeyContextFactory().CreateDbContext([]);

        Assert.NotEmpty(app.Database.GetMigrations());
        Assert.NotEmpty(dataProtection.Database.GetMigrations());
        Assert.False(app.Database.HasPendingModelChanges());
        Assert.False(dataProtection.Database.HasPendingModelChanges());
    }
}
