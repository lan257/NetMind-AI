using System.Text.Json;

namespace NetMind.Models.Dtos;

public sealed class AiModelOptionDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;
}

public sealed class AiCleanRequest
{
    public string NaturalLanguage { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? ApiKey { get; set; }

    public string? Endpoint { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }
}

public sealed class AiRequirementStructureRequest
{
    public string Requirement { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? ApiKey { get; set; }

    public string? Endpoint { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }
}

public sealed class AiContextChatRequest
{
    public string ConversationId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string? ModelId { get; init; }

    public string? ApiKey { get; set; }

    public string? Endpoint { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }
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

public sealed class AiNodeChatRequest
{
    public long NodeId { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string? ConversationId { get; init; }

    public string? ModelId { get; init; }

    public string? ApiKey { get; set; }

    public int MaxContextLength { get; init; } = 51200;

    public string? Endpoint { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }
}

public sealed class AiNodeChatResult
{
    public AiModelOptionDto SelectedModel { get; init; } = new();

    public string Prompt { get; init; } = string.Empty;

    public string Reply { get; init; } = string.Empty;

    public string CompressedContext { get; init; } = string.Empty;

    public bool WasContextCompressed { get; init; }

    public double ContextUsagePercent { get; init; }

    public string ContextStatus { get; init; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public class AiAgentChatRequest
{
    public string Message { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string? ConversationId { get; init; }

    public string? ModelId { get; init; }

    public string? ApiKey { get; set; }

    public string? Endpoint { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }

    public int MaxContextLength { get; init; } = 51200;

    public string? AgentBuildPath { get; init; }

    public string? Domain { get; set; }

    public JsonElement? AgentContext { get; init; }

    public IReadOnlyList<JsonElement> ConfirmedToolCalls { get; init; } = Array.Empty<JsonElement>();

    public IReadOnlyList<JsonElement> HistoryToolCalls { get; init; } = Array.Empty<JsonElement>();
}

public sealed class AiNodeAgentChatRequest : AiAgentChatRequest
{
    public long NodeId { get; init; }
}

public sealed class AiMapAgentChatRequest : AiAgentChatRequest
{
    public long MapId { get; init; }
}

public sealed class AiGlobalAgentChatRequest : AiAgentChatRequest
{
}

public sealed class AiAppHelpAgentChatRequest : AiAgentChatRequest
{
}

public sealed class AiAgentChatResult
{
    public AiModelOptionDto SelectedModel { get; init; } = new();

    public string Prompt { get; init; } = string.Empty;

    public string Reply { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string AgentTarget { get; init; } = string.Empty;

    public IReadOnlyList<JsonElement> ToolCalls { get; init; } = Array.Empty<JsonElement>();

    public JsonElement ContextUpdate { get; init; }

    public string CompressedContext { get; init; } = string.Empty;

    public bool WasContextCompressed { get; init; }

    public double ContextUsagePercent { get; init; }

    public string ContextStatus { get; init; } = string.Empty;

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

public sealed class AiMapChatRequest
{
    public long MapId { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string? ConversationId { get; init; }

    public string? ModelId { get; init; }

    public string? ApiKey { get; set; }

    public int MaxContextLength { get; init; } = 51200;

    public string? Endpoint { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }
}

public sealed class AiMapChatResult
{
    public AiModelOptionDto SelectedModel { get; init; } = new();

    public string Prompt { get; init; } = string.Empty;

    public string Reply { get; init; } = string.Empty;

    public string CompressedContext { get; init; } = string.Empty;

    public bool WasContextCompressed { get; init; }

    public double ContextUsagePercent { get; init; }

    public string ContextStatus { get; init; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class AiAppHelpRequest
{
    public string Message { get; init; } = string.Empty;

    public string Context { get; init; } = string.Empty;

    public string? ConversationId { get; init; }

    public string? ModelId { get; init; }

    public string? ApiKey { get; set; }

    public int MaxContextLength { get; init; } = 51200;

    public string? Endpoint { get; init; }

    public string? Provider { get; init; }

    public string? Model { get; init; }
}

public sealed class AiAppHelpResult
{
    public AiModelOptionDto SelectedModel { get; init; } = new();

    public string Prompt { get; init; } = string.Empty;

    public string Reply { get; init; } = string.Empty;

    public string CompressedContext { get; init; } = string.Empty;

    public bool WasContextCompressed { get; init; }

    public double ContextUsagePercent { get; init; }

    public string ContextStatus { get; init; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
