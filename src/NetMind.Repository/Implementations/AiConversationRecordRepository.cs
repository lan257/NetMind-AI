using NetMind.Common.Logging;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using Npgsql;

namespace NetMind.Repository.Implementations;

public sealed class AiConversationRecordRepository : IAiConversationRecordRepository
{
    private readonly PostgresConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public AiConversationRecordRepository(PostgresConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiConversationRecordEntity>> ListAsync(string? conversationId)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        var hasConversationId = !string.IsNullOrWhiteSpace(conversationId);
        await using var command = new NpgsqlCommand(
            $"""
            SELECT id, conversation_id, role, content, model_id, prompt, context_summary,
                   was_context_compressed, created_at, updated_at, is_deleted, deleted_at
            FROM ai_conversation_record
            WHERE is_deleted = FALSE
              {(hasConversationId ? "AND conversation_id = @conversation_id" : string.Empty)}
            ORDER BY created_at, id;
            """,
            connection);

        if (hasConversationId)
        {
            command.Parameters.AddWithValue("conversation_id", conversationId!.Trim());
        }

        var result = new List<AiConversationRecordEntity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadRecord(reader));
        }

        return result;
    }

    public async Task<AiConversationRecordEntity?> GetAsync(long id)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT id, conversation_id, role, content, model_id, prompt, context_summary,
                   was_context_compressed, created_at, updated_at, is_deleted, deleted_at
            FROM ai_conversation_record
            WHERE id = @id AND is_deleted = FALSE;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadRecord(reader) : null;
    }

    public async Task<AiConversationRecordEntity> CreateAsync(
        string conversationId,
        string role,
        string content,
        string? modelId,
        string? prompt,
        string? contextSummary,
        bool wasContextCompressed)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO ai_conversation_record
                (conversation_id, role, content, model_id, prompt, context_summary,
                 was_context_compressed, created_at, updated_at)
            VALUES
                (@conversation_id, @role, @content, @model_id, @prompt, @context_summary,
                 @was_context_compressed, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            RETURNING id, conversation_id, role, content, model_id, prompt, context_summary,
                      was_context_compressed, created_at, updated_at, is_deleted, deleted_at;
            """,
            connection);
        AddParameters(command, conversationId, role, content, modelId, prompt, contextSummary, wasContextCompressed);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("AI 对话记录创建失败。");
        }

        var created = ReadRecord(reader);
        LogWriteOperation("新增 AI 对话记录", created.Id, 1);
        return created;
    }

    public async Task<AiConversationRecordEntity?> UpdateAsync(
        long id,
        string role,
        string content,
        string? modelId,
        string? prompt,
        string? contextSummary,
        bool wasContextCompressed)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE ai_conversation_record
            SET role = @role,
                content = @content,
                model_id = @model_id,
                prompt = @prompt,
                context_summary = @context_summary,
                was_context_compressed = @was_context_compressed,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id AND is_deleted = FALSE
            RETURNING id, conversation_id, role, content, model_id, prompt, context_summary,
                      was_context_compressed, created_at, updated_at, is_deleted, deleted_at;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("model_id", (object?)modelId ?? DBNull.Value);
        command.Parameters.AddWithValue("prompt", (object?)prompt ?? DBNull.Value);
        command.Parameters.AddWithValue("context_summary", (object?)contextSummary ?? DBNull.Value);
        command.Parameters.AddWithValue("was_context_compressed", wasContextCompressed);

        await using var reader = await command.ExecuteReaderAsync();
        var updated = await reader.ReadAsync() ? ReadRecord(reader) : null;
        LogWriteOperation("更新 AI 对话记录", id, updated is null ? 0 : 1);
        return updated;
    }

    public async Task<int> DeleteAsync(long id)
    {
        await using var connection = await _connectionFactory.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE ai_conversation_record
            SET is_deleted = TRUE,
                deleted_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id AND is_deleted = FALSE;
            """,
            connection);
        command.Parameters.AddWithValue("id", id);

        var affected = await command.ExecuteNonQueryAsync();
        LogWriteOperation("删除 AI 对话记录", id, affected);
        return affected;
    }

    private static void AddParameters(
        NpgsqlCommand command,
        string conversationId,
        string role,
        string content,
        string? modelId,
        string? prompt,
        string? contextSummary,
        bool wasContextCompressed)
    {
        command.Parameters.AddWithValue("conversation_id", conversationId);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("model_id", (object?)modelId ?? DBNull.Value);
        command.Parameters.AddWithValue("prompt", (object?)prompt ?? DBNull.Value);
        command.Parameters.AddWithValue("context_summary", (object?)contextSummary ?? DBNull.Value);
        command.Parameters.AddWithValue("was_context_compressed", wasContextCompressed);
    }

    private void LogWriteOperation(string operation, long recordId, int affected)
    {
        _logger.Info("存储层写操作", operation, new Dictionary<string, object?>
        {
            ["Operation"] = operation,
            ["RecordId"] = recordId,
            ["Affected"] = affected
        });
    }

    private static AiConversationRecordEntity ReadRecord(NpgsqlDataReader reader)
    {
        return new AiConversationRecordEntity
        {
            Id = reader.GetInt64(0),
            ConversationId = reader.GetString(1),
            Role = reader.GetString(2),
            Content = reader.GetString(3),
            ModelId = reader.IsDBNull(4) ? null : reader.GetString(4),
            Prompt = reader.IsDBNull(5) ? null : reader.GetString(5),
            ContextSummary = reader.IsDBNull(6) ? null : reader.GetString(6),
            WasContextCompressed = reader.GetBoolean(7),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(8),
            UpdatedAt = reader.GetFieldValue<DateTimeOffset>(9),
            IsDeleted = reader.GetBoolean(10),
            DeletedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11)
        };
    }
}
