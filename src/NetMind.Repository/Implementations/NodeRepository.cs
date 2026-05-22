using NetMind.Common.Logging;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using Npgsql;

namespace NetMind.Repository.Implementations;

public sealed class NodeRepository : INodeRepository
{
    private readonly PostgresConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public NodeRepository(PostgresConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NodeEntity>> ListByMapAsync(long mapId)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT n.id, n.map_id, n.parent_id, n.title, n.content, n.order_no, n.position_x, n.position_y, n.created_at, n.updated_at, n.is_deleted, n.deleted_at,
                   m.title as map_title
            FROM node n
            LEFT JOIN mind_map m ON n.map_id = m.id
            WHERE n.map_id = @map_id AND n.is_deleted = FALSE
            ORDER BY n.parent_id NULLS FIRST, n.order_no, n.id;
            """,
            connection);
        command.Parameters.AddWithValue("map_id", mapId);

        var result = new List<NodeEntity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadNode(reader));
        }

        return result;
    }

    public async Task<IReadOnlyList<NodeEntity>> SearchAsync(long? mapId, string keyword, int limit)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        var sql = """
            SELECT n.id, n.map_id, n.parent_id, n.title, n.content, n.order_no, n.position_x, n.position_y, n.created_at, n.updated_at, n.is_deleted, n.deleted_at,
                   m.title as map_title
            FROM node n
            LEFT JOIN mind_map m ON n.map_id = m.id
            WHERE n.is_deleted = FALSE 
              AND (n.title ILIKE @keyword OR n.content ILIKE @keyword)
            """;

        if (mapId.HasValue)
        {
            sql += " AND n.map_id = @map_id";
        }

        sql += """
            ORDER BY n.updated_at DESC
            LIMIT @limit;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        if (mapId.HasValue)
        {
            command.Parameters.AddWithValue("map_id", mapId.Value);
        }
        command.Parameters.AddWithValue("keyword", $"%{keyword}%");
        command.Parameters.AddWithValue("limit", limit);

        var result = new List<NodeEntity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadNode(reader));
        }

        return result;
    }

    public async Task<NodeEntity?> GetAsync(long id)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT n.id, n.map_id, n.parent_id, n.title, n.content, n.order_no, n.position_x, n.position_y, n.created_at, n.updated_at, n.is_deleted, n.deleted_at,
                   m.title as map_title
            FROM node n
            LEFT JOIN mind_map m ON n.map_id = m.id
            WHERE n.id = @id AND n.is_deleted = FALSE;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadNode(reader) : null;
    }

    public async Task<bool> ExistsSiblingOrderNoAsync(long mapId, long? parentId, int orderNo, long excludeNodeId)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM node
                WHERE map_id = @map_id
                  AND parent_id IS NOT DISTINCT FROM @parent_id
                  AND order_no = @order_no
                  AND id <> @exclude_node_id
                  AND is_deleted = FALSE
            );
            """,
            connection);
        command.Parameters.AddWithValue("map_id", mapId);
        command.Parameters.Add("parent_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = (object?)parentId ?? DBNull.Value;
        command.Parameters.AddWithValue("order_no", orderNo);
        command.Parameters.AddWithValue("exclude_node_id", excludeNodeId);

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    public async Task<NodeEntity> CreateAsync(long mapId, long? parentId, string title, string? content, int orderNo, double? positionX, double? positionY)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (!await MapExistsAsync(connection, transaction, mapId))
        {
            throw new InvalidOperationException("导图不存在。");
        }

        if (parentId.HasValue && !await NodeExistsAsync(connection, transaction, mapId, parentId.Value))
        {
            throw new InvalidOperationException("父节点不在同一导图中或已不存在。");
        }

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO node (map_id, parent_id, title, content, order_no, position_x, position_y, created_at, updated_at)
            VALUES (@map_id, @parent_id, @title, @content, @order_no, @position_x, @position_y, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            RETURNING id, map_id, parent_id, title, content, order_no, position_x, position_y, created_at, updated_at, is_deleted, deleted_at;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("map_id", mapId);
        command.Parameters.AddWithValue("parent_id", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("content", (object?)content ?? DBNull.Value);
        command.Parameters.AddWithValue("order_no", orderNo);
        command.Parameters.Add("position_x", NpgsqlTypes.NpgsqlDbType.Double).Value = (object?)positionX ?? DBNull.Value;
        command.Parameters.Add("position_y", NpgsqlTypes.NpgsqlDbType.Double).Value = (object?)positionY ?? DBNull.Value;

        NodeEntity created;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("节点创建失败。");
            }

            created = ReadNode(reader);
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE mind_map
            SET root_node_id = COALESCE(root_node_id, @node_id),
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @map_id AND is_deleted = FALSE;
            """,
            ("map_id", mapId),
            ("node_id", created.Id));

        await transaction.CommitAsync();
        LogWriteOperation("新增节点", new Dictionary<string, object?>
        {
            ["NodeId"] = created.Id,
            ["MindMapId"] = mapId,
            ["ParentId"] = parentId
        });

        return created;
    }

    public async Task<NodeEntity?> UpdateAsync(long id, long? parentId, string title, string? content, int orderNo, double? positionX, double? positionY)
    {
        if (parentId == id)
        {
            return null;
        }

        await using var connection = await _connectionFactory.OpenAsync();
        var current = await GetNodeForUpdateAsync(connection, id);
        if (current is null)
        {
            return null;
        }

        if (parentId.HasValue && !await NodeExistsAsync(connection, null, current.MapId, parentId.Value))
        {
            return null;
        }

        await using var command = new NpgsqlCommand(
            """
            UPDATE node
            SET parent_id = @parent_id,
                title = @title,
                content = @content,
                order_no = @order_no,
                position_x = @position_x,
                position_y = @position_y,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id AND is_deleted = FALSE
            RETURNING id, map_id, parent_id, title, content, order_no, position_x, position_y, created_at, updated_at, is_deleted, deleted_at;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("parent_id", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("content", (object?)content ?? DBNull.Value);
        command.Parameters.AddWithValue("order_no", orderNo);
        command.Parameters.Add("position_x", NpgsqlTypes.NpgsqlDbType.Double).Value = (object?)positionX ?? DBNull.Value;
        command.Parameters.Add("position_y", NpgsqlTypes.NpgsqlDbType.Double).Value = (object?)positionY ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync();
        var updated = await reader.ReadAsync() ? ReadNode(reader) : null;
        LogWriteOperation("更新节点", new Dictionary<string, object?>
        {
            ["NodeId"] = id,
            ["Affected"] = updated is null ? 0 : 1
        });

        return updated;
    }

    public async Task<int> DeleteSelfAsync(long id)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var node = await GetNodeForUpdateAsync(connection, id, transaction);
        if (node is null)
        {
            await transaction.RollbackAsync();
            return 0;
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE node
            SET parent_id = @parent_id,
                updated_at = CURRENT_TIMESTAMP
            WHERE parent_id = @id AND is_deleted = FALSE;
            """,
            ("id", id),
            ("parent_id", node.ParentId));

        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE node_relation
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP
            WHERE is_deleted = FALSE AND (source_id = @id OR target_id = @id);
            """,
            ("id", id));

        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE node_meta
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP
            WHERE node_id = @id AND is_deleted = FALSE;
            """,
            ("id", id));

        var affected = await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE node
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id AND is_deleted = FALSE;
            """,
            ("id", id));

        await RefreshRootNodeAsync(connection, transaction, node.MapId, id);
        await transaction.CommitAsync();
        LogWriteOperation("删除节点", new Dictionary<string, object?>
        {
            ["NodeId"] = id,
            ["Mode"] = "self",
            ["Affected"] = affected
        });

        return affected;
    }

    public async Task<int> DeleteSubtreeAsync(long id)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var root = await GetNodeForUpdateAsync(connection, id, transaction);
        if (root is null)
        {
            await transaction.RollbackAsync();
            return 0;
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE node_relation
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP
            WHERE is_deleted = FALSE
              AND (
                  source_id IN (
                      WITH RECURSIVE subtree AS (
                          SELECT id FROM node WHERE id = @id AND is_deleted = FALSE
                          UNION ALL
                          SELECT child.id FROM node child
                          JOIN subtree parent ON child.parent_id = parent.id
                          WHERE child.is_deleted = FALSE
                      )
                      SELECT id FROM subtree
                  )
                  OR target_id IN (
                      WITH RECURSIVE subtree AS (
                          SELECT id FROM node WHERE id = @id AND is_deleted = FALSE
                          UNION ALL
                          SELECT child.id FROM node child
                          JOIN subtree parent ON child.parent_id = parent.id
                          WHERE child.is_deleted = FALSE
                      )
                      SELECT id FROM subtree
                  )
              );
            """,
            ("id", id));

        await ExecuteAsync(
            connection,
            transaction,
            """
            WITH RECURSIVE subtree AS (
                SELECT id FROM node WHERE id = @id AND is_deleted = FALSE
                UNION ALL
                SELECT child.id FROM node child
                JOIN subtree parent ON child.parent_id = parent.id
                WHERE child.is_deleted = FALSE
            )
            UPDATE node_meta
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP
            WHERE is_deleted = FALSE
              AND node_id IN (SELECT id FROM subtree);
            """,
            ("id", id));

        var affected = await ExecuteAsync(
            connection,
            transaction,
            """
            WITH RECURSIVE subtree AS (
                SELECT id FROM node WHERE id = @id AND is_deleted = FALSE
                UNION ALL
                SELECT child.id FROM node child
                JOIN subtree parent ON child.parent_id = parent.id
                WHERE child.is_deleted = FALSE
            )
            UPDATE node
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            WHERE id IN (SELECT id FROM subtree)
              AND is_deleted = FALSE;
            """,
            ("id", id));

        await RefreshRootNodeAsync(connection, transaction, root.MapId, id);
        await transaction.CommitAsync();
        LogWriteOperation("删除节点子树", new Dictionary<string, object?>
        {
            ["NodeId"] = id,
            ["Mode"] = "subtree",
            ["Affected"] = affected
        });

        return affected;
    }

    private void LogWriteOperation(string operation, IReadOnlyDictionary<string, object?> properties)
    {
        var values = new Dictionary<string, object?>(properties)
        {
            ["Operation"] = operation
        };
        _logger.Info("存储层写操作", operation, values);
    }

    private static async Task<bool> MapExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, long mapId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM mind_map WHERE id = @map_id AND is_deleted = FALSE);",
            connection,
            transaction);
        command.Parameters.AddWithValue("map_id", mapId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<bool> NodeExistsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, long mapId, long nodeId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM node WHERE id = @node_id AND map_id = @map_id AND is_deleted = FALSE);",
            connection,
            transaction);
        command.Parameters.AddWithValue("node_id", nodeId);
        command.Parameters.AddWithValue("map_id", mapId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<NodeEntity?> GetNodeForUpdateAsync(
        NpgsqlConnection connection,
        long id,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, map_id, parent_id, title, content, order_no, position_x, position_y, created_at, updated_at, is_deleted, deleted_at
            FROM node
            WHERE id = @id AND is_deleted = FALSE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadNode(reader) : null;
    }

    private static async Task RefreshRootNodeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long mapId,
        long deletedNodeId)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE mind_map
            SET root_node_id = (
                    SELECT id
                    FROM node
                    WHERE map_id = @map_id
                      AND parent_id IS NULL
                      AND is_deleted = FALSE
                    ORDER BY order_no, id
                    LIMIT 1
                ),
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @map_id
              AND root_node_id = @deleted_node_id
              AND is_deleted = FALSE;
            """,
            ("map_id", mapId),
            ("deleted_node_id", deletedNodeId));
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

    private static NodeEntity ReadNode(NpgsqlDataReader reader)
    {
        var entity = new NodeEntity
        {
            Id = reader.GetInt64(0),
            MapId = reader.GetInt64(1),
            ParentId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
            Title = reader.GetString(3),
            Content = reader.IsDBNull(4) ? null : reader.GetString(4),
            OrderNo = reader.GetInt32(5),
            PositionX = reader.IsDBNull(6) ? null : reader.GetDouble(6),
            PositionY = reader.IsDBNull(7) ? null : reader.GetDouble(7),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(8),
            UpdatedAt = reader.GetFieldValue<DateTimeOffset>(9),
            IsDeleted = reader.GetBoolean(10),
            DeletedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11)
        };

        if (reader.FieldCount > 12 && !reader.IsDBNull(12))
        {
            entity.MapTitle = reader.GetString(12);
        }

        return entity;
    }
}
