using Npgsql;

namespace NetMind.Repository.Implementations;

public sealed class PostgresConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("必须配置 PGSTR 环境变量作为数据库连接字符串。");
        }

        _connectionString = connectionString;
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
