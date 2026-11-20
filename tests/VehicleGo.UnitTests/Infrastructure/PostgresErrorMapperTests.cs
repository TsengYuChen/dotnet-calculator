using Microsoft.EntityFrameworkCore;
using Npgsql;
using VehicleGo.Domain.Common;
using VehicleGo.Infrastructure.Persistence;

namespace VehicleGo.UnitTests.Infrastructure;

/// <summary>Design §2.4: database errors that are concurrent-write fallbacks become error codes.</summary>
public class PostgresErrorMapperTests
{
    private static DbUpdateException Failed(string sqlState, string? constraint) =>
        new("failed", new PostgresException("failed", "ERROR", "ERROR", sqlState, constraintName: constraint));

    [Fact]
    public void 版本號衝突轉成VEHICLE_MODIFIED() =>
        Assert.Equal(ErrorCode.VehicleModified, PostgresErrorMapper.TryMap(new DbUpdateConcurrencyException("conflict"))?.Code);

    [Fact]
    public void 車牌唯一索引衝突轉成PLATE_EXISTS_ACTIVE() =>
        Assert.Equal(ErrorCode.PlateExistsActive, PostgresErrorMapper.TryMap(Failed("23505", "ux_vehicles_owner_plate"))?.Code);

    [Theory]
    [InlineData("23505", "ux_users_google_sub")]
    [InlineData("23514", "ck_vehicles_bracket")]
    [InlineData("23503", "fk_vehicles_owner")]
    [InlineData("40001", null)]
    public void 其他資料庫錯誤不轉換(string sqlState, string? constraint) =>
        Assert.Null(PostgresErrorMapper.TryMap(Failed(sqlState, constraint)));

    [Fact]
    public void 沒有PostgresException時不轉換() =>
        Assert.Null(PostgresErrorMapper.TryMap(new DbUpdateException("failed", new InvalidOperationException())));
}
