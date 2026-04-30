namespace NetMind.Models.Dtos;

public sealed class AiConversationRecordDto
{
    public long Id { get; init; }

    public string ConversationId { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? Prompt { get; init; }

    public string? ContextSummary { get; init; }

    public bool WasContextCompressed { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class CreateAiConversationRecordRequest
{
    public string ConversationId { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? Prompt { get; init; }

    public string? ContextSummary { get; init; }

    public bool WasContextCompressed { get; init; }
}

public sealed class UpdateAiConversationRecordRequest
{
    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? Prompt { get; init; }

    public string? ContextSummary { get; init; }

    public bool WasContextCompressed { get; init; }
}
