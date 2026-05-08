using NetMind.Common.Logging;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using Npgsql;

namespace NetMind.Repository.Implementations;

public sealed class NodeRelationRepository : INodeRelationRepository
{
    private readonly PostgresConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public NodeRelationRepository(PostgresConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NodeRelationEntity>> ListByMapAsync(long mapId)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT r.id, r.source_id, r.target_id, r.relation_type, r.weight, r.map_id, r.created_at, r.is_deleted, r.deleted_at,
                   s.title as source_title, t.title as target_title,
                   s.map_id as source_map_id, t.map_id as target_map_id
            FROM node_relation r
            JOIN node s ON r.source_id = s.id
            JOIN node t ON r.target_id = t.id
            WHERE r.map_id = @map_id AND r.is_deleted = FALSE
            ORDER BY r.id;
            """,
            connection);
        command.Parameters.AddWithValue("map_id", mapId);

        var result = new List<NodeRelationEntity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadRelation(reader));
        }

        return result;
    }

    public async Task<IReadOnlyList<NodeRelationEntity>> ListBySourceAsync(long sourceId)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT r.id, r.source_id, r.target_id, r.relation_type, r.weight, r.map_id, r.created_at, r.is_deleted, r.deleted_at,
                   s.title as source_title, t.title as target_title,
                   s.map_id as source_map_id, t.map_id as target_map_id
            FROM node_relation r
            JOIN node s ON r.source_id = s.id
            JOIN node t ON r.target_id = t.id
            WHERE r.source_id = @source_id AND r.is_deleted = FALSE
            ORDER BY r.id;
            """,
            connection);
        command.Parameters.AddWithValue("source_id", sourceId);

        var result = new List<NodeRelationEntity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadRelation(reader));
        }

        return result;
    }

    public async Task<IReadOnlyList<NodeRelationEntity>> ListByNodeAsync(long nodeId)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT r.id, r.source_id, r.target_id, r.relation_type, r.weight, r.map_id, r.created_at, r.is_deleted, r.deleted_at,
                   s.title as source_title, t.title as target_title,
                   s.map_id as source_map_id, t.map_id as target_map_id
            FROM node_relation r
            JOIN node s ON r.source_id = s.id
            JOIN node t ON r.target_id = t.id
            WHERE (r.source_id = @node_id OR r.target_id = @node_id) AND r.is_deleted = FALSE
            ORDER BY r.id;
            """,
            connection);
        command.Parameters.AddWithValue("node_id", nodeId);

        var result = new List<NodeRelationEntity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadRelation(reader));
        }

        return result;
    }

    public async Task<NodeRelationEntity?> GetAsync(long id)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT r.id, r.source_id, r.target_id, r.relation_type, r.weight, r.map_id, r.created_at, r.is_deleted, r.deleted_at,
                   s.title as source_title, t.title as target_title,
                   s.map_id as source_map_id, t.map_id as target_map_id
            FROM node_relation r
            JOIN node s ON r.source_id = s.id
            JOIN node t ON r.target_id = t.id
            WHERE r.id = @id AND r.is_deleted = FALSE;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadRelation(reader) : null;
    }

    public async Task<NodeRelationEntity> CreateAsync(long sourceId, long targetId, string relationType, double weight, long mapId)
    {
        if (sourceId == targetId)
        {
            throw new InvalidOperationException("源节点和目标节点不能相同。");
        }

        await using var connection = await _connectionFactory.OpenAsync();
        var endpointCount = await CountExistingEndpointsAsync(connection, sourceId, targetId);
        if (endpointCount != 2)
        {
            throw new InvalidOperationException("源节点或目标节点不存在。");
        }

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO node_relation (source_id, target_id, relation_type, weight, map_id, created_at)
            VALUES (@source_id, @target_id, @relation_type, @weight, @map_id, CURRENT_TIMESTAMP)
            RETURNING id, source_id, target_id, relation_type, weight, map_id, created_at, is_deleted, deleted_at;
            """,
            connection);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("target_id", targetId);
        command.Parameters.AddWithValue("relation_type", relationType);
        command.Parameters.AddWithValue("weight", weight);
        command.Parameters.AddWithValue("map_id", mapId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("节点关联创建失败。");
        }

        var created = ReadRelation(reader);
        LogWriteOperation("新增节点关联", new Dictionary<string, object?>
        {
            ["RelationId"] = created.Id,
            ["MindMapId"] = mapId,
            ["SourceId"] = sourceId,
            ["TargetId"] = targetId
        });

        return created;
    }

    public async Task<NodeRelationEntity?> UpdateAsync(long id, string relationType, double weight)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE node_relation
            SET relation_type = @relation_type,
                weight = @weight
            WHERE id = @id AND is_deleted = FALSE
            RETURNING id, source_id, target_id, relation_type, weight, map_id, created_at, is_deleted, deleted_at;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("relation_type", relationType);
        command.Parameters.AddWithValue("weight", weight);

        await using var reader = await command.ExecuteReaderAsync();
        var updated = await reader.ReadAsync() ? ReadRelation(reader) : null;
        LogWriteOperation("更新节点关联", new Dictionary<string, object?>
        {
            ["RelationId"] = id,
            ["Affected"] = updated is null ? 0 : 1
        });

        return updated;
    }

    public async Task<int> DeleteAsync(long id)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE node_relation
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP
            WHERE id = @id AND is_deleted = FALSE;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);

        var affected = await command.ExecuteNonQueryAsync();
        LogWriteOperation("删除节点关联", new Dictionary<string, object?>
        {
            ["RelationId"] = id,
            ["Affected"] = affected
        });

        return affected;
    }

    public async Task<int> DeleteByNodeAsync(long nodeId)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE node_relation
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP
            WHERE is_deleted = FALSE AND (source_id = @node_id OR target_id = @node_id);
            """,
            connection);
        command.Parameters.AddWithValue("node_id", nodeId);

        var affected = await command.ExecuteNonQueryAsync();
        LogWriteOperation("按节点删除关联", new Dictionary<string, object?>
        {
            ["NodeId"] = nodeId,
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

    private static async Task<long> CountExistingEndpointsAsync(NpgsqlConnection connection, long sourceId, long targetId)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM node
            WHERE id IN (@source_id, @target_id)
              AND is_deleted = FALSE;
            """,
            connection);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("target_id", targetId);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static NodeRelationEntity ReadRelation(NpgsqlDataReader reader)
    {
        var entity = new NodeRelationEntity
        {
            Id = reader.GetInt64(0),
            SourceId = reader.GetInt64(1),
            TargetId = reader.GetInt64(2),
            RelationType = reader.GetString(3),
            Weight = reader.GetDouble(4),
            MapId = reader.GetInt64(5),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
            IsDeleted = reader.GetBoolean(7),
            DeletedAt = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8)
        };

        // 读取可选的标题和导图ID（如果 SQL 中包含 JOIN）
        for (int i = 9; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i).ToLowerInvariant();
            if (name == "source_title" && !reader.IsDBNull(i)) entity.SourceTitle = reader.GetString(i);
            else if (name == "target_title" && !reader.IsDBNull(i)) entity.TargetTitle = reader.GetString(i);
            else if (name == "source_map_id" && !reader.IsDBNull(i)) entity.SourceMapId = reader.GetInt64(i);
            else if (name == "target_map_id" && !reader.IsDBNull(i)) entity.TargetMapId = reader.GetInt64(i);
        }

        return entity;
    }
}
