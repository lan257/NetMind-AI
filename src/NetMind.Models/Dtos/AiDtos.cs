namespace NetMind.Models.Dtos;

public sealed class AiModelOptionDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;
}

public sealed class AiCleanRequest
{
    public string NaturalLanguage { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? ApiKey { get; init; }
}

public sealed class AiRequirementStructureRequest
{
    public string Requirement { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? ApiKey { get; init; }
}

public sealed class AiContextChatRequest
{
    public string ConversationId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? ApiKey { get; init; }
}

public sealed class AiCleanResultDto
{
    public AiModelOptionDto SelectedModel { get; init; } = new();

    public string Prompt { get; init; } = string.Empty;

    public MindMapTransferDto Transfer { get; init; } = new();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class AiContextChatResultDto
{
    public AiModelOptionDto SelectedModel { get; init; } = new();

    public string Prompt { get; init; } = string.Empty;

    public string Reply { get; init; } = string.Empty;

    public string ContextSummary { get; init; } = string.Empty;

    public bool WasContextCompressed { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class AiRequirementStructureResultDto
{
    public AiModelOptionDto SelectedModel { get; init; } = new();

    public string Prompt { get; init; } = string.Empty;

    public string ContextSummary { get; init; } = string.Empty;

    public bool WasContextCompressed { get; init; }

    public MindMapTransferDto Transfer { get; init; } = new();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}