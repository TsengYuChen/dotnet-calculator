using Npgsql;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>Direct SQL against the test database (to set up or inspect state the API cannot).</summary>
public sealed class Database(string connectionString)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync(Ct);
    }

    public async Task<long> CountAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    public Task<long> UsersWithSubAsync(string subject) =>
        CountAsync("SELECT count(*) FROM users WHERE google_sub = @s", ("s", subject));

    public Task<long> VehicleRowsAsync(Guid id) =>
        CountAsync("SELECT count(*) FROM vehicles WHERE id = @id", ("id", id));

    public Task SetDeletedAtAsync(Guid id, DateTimeOffset deletedAt) =>
        ExecuteAsync("UPDATE vehicles SET deleted_at = @d WHERE id = @id", ("d", deletedAt), ("id", id));
}
