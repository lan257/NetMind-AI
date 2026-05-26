using Npgsql;

namespace NetMind.Repository.Implementations;

public sealed class PostgresConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("必须在配置文件的 ConnectionStrings:Postgres 中配置 PostgreSQL 连接字符串。");
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
