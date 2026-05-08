using System.Diagnostics;
using NetMind.Common.Logging;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using Npgsql;

namespace NetMind.Repository.Implementations;

public sealed class MindMapRepository : IMindMapRepository
{
    private readonly PostgresConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public MindMapRepository(PostgresConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MindMapEntity>> ListAsync()
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT id, title, root_node_id, created_at, updated_at, is_deleted, deleted_at
            FROM mind_map
            WHERE is_deleted = FALSE
            ORDER BY id;
            """,
            connection);

        var result = new List<MindMapEntity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadMindMap(reader));
        }

        return result;
    }

    public async Task<MindMapEntity?> GetAsync(long id)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT id, title, root_node_id, created_at, updated_at, is_deleted, deleted_at
            FROM mind_map
            WHERE id = @id AND is_deleted = FALSE;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadMindMap(reader) : null;
    }

    public async Task<MindMapEntity> CreateAsync(string title)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO mind_map (title, created_at, updated_at)
            VALUES (@title, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            RETURNING id, title, root_node_id, created_at, updated_at, is_deleted, deleted_at;
            """,
            connection);
        command.Parameters.AddWithValue("title", title);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("导图创建失败。");
        }

        var created = ReadMindMap(reader);
        LogWriteOperation("新增导图", stopwatch: null, new Dictionary<string, object?>
        {
            ["MindMapId"] = created.Id,
            ["Title"] = title
        });

        return created;
    }

    public async Task<MindMapEntity?> UpdateAsync(long id, string title, long? rootNodeId)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        if (rootNodeId.HasValue && !await NodeExistsAsync(connection, id, rootNodeId.Value))
        {
            return null;
        }

        await using var command = new NpgsqlCommand(
            """
            UPDATE mind_map
            SET title = @title,
                root_node_id = @root_node_id,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id AND is_deleted = FALSE
            RETURNING id, title, root_node_id, created_at, updated_at, is_deleted, deleted_at;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("root_node_id", (object?)rootNodeId ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        var updated = await reader.ReadAsync() ? ReadMindMap(reader) : null;
        LogWriteOperation("更新导图", stopwatch: null, new Dictionary<string, object?>
        {
            ["MindMapId"] = id,
            ["Affected"] = updated is null ? 0 : 1
        });

        return updated;
    }

    public async Task<int> DeleteAsync(long id, bool cascade)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var affected = await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE mind_map
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id AND is_deleted = FALSE;
            """,
            ("id", id));

        if (affected == 0)
        {
            await transaction.RollbackAsync();
            return 0;
        }

        // Always delete nodes and relations belonging to this map
        affected += await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE node
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            WHERE map_id = @id AND is_deleted = FALSE;
            """,
            ("id", id));

        affected += await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE node_relation
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP
            WHERE map_id = @id AND is_deleted = FALSE;
            """,
            ("id", id));

        affected += await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE node_meta
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP
            WHERE is_deleted = FALSE 
              AND node_id IN (SELECT id FROM node WHERE map_id = @id);
            """,
            ("id", id));

        if (cascade)
        {
            // Cascade delete: handle relations in OTHER maps that point to nodes in THIS map
            affected += await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE node_relation
                SET is_deleted = TRUE,
                    deleted_at = CURRENT_TIMESTAMP
                WHERE is_deleted = FALSE
                  AND map_id <> @id
                  AND (
                      source_id IN (SELECT id FROM node WHERE map_id = @id)
                      OR target_id IN (SELECT id FROM node WHERE map_id = @id)
                  );
                """,
                ("id", id));
        }

        await transaction.CommitAsync();
        LogWriteOperation("删除导图", stopwatch: null, new Dictionary<string, object?>
        {
            ["MindMapId"] = id,
            ["Cascade"] = cascade,
            ["Affected"] = affected
        });

        return affected;
    }

    private void LogWriteOperation(string operation, Stopwatch? stopwatch, IReadOnlyDictionary<string, object?> properties)
    {
        var values = new Dictionary<string, object?>(properties)
        {
            ["Operation"] = operation
        };

        if (stopwatch is not null)
        {
            values["ElapsedMs"] = stopwatch.ElapsedMilliseconds;
        }

        _logger.Info("存储层写操作", operation, values);
    }

    private static async Task<bool> NodeExistsAsync(NpgsqlConnection connection, long mapId, long nodeId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM node WHERE id = @node_id AND map_id = @map_id AND is_deleted = FALSE);",
            connection);
        command.Parameters.AddWithValue("node_id", nodeId);
        command.Parameters.AddWithValue("map_id", mapId);

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    private static MindMapEntity ReadMindMap(NpgsqlDataReader reader)
    {
        return new MindMapEntity
        {
            Id = reader.GetInt64(0),
            Title = reader.GetString(1),
            RootNodeId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(3),
            UpdatedAt = reader.GetFieldValue<DateTimeOffset>(4),
            IsDeleted = reader.GetBoolean(5),
            DeletedAt = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)
        };
    }
}
