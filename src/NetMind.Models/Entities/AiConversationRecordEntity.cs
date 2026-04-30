namespace NetMind.Models.Entities;

public sealed class AiConversationRecordEntity
{
    public long Id { get; set; }

    public string ConversationId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? ModelId { get; set; }

    public string? Prompt { get; set; }

    public string? ContextSummary { get; set; }

    public bool WasContextCompressed { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
