namespace NetMind.Services.Configurations;

public sealed class AiCleanOptions
{
    public IReadOnlyList<AiModelOptions> Models { get; init; } = Array.Empty<AiModelOptions>();

    public AiPromptOptions Prompt { get; init; } = new();
}

public sealed class AiModelOptions
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public bool IsDefault { get; init; }

    public string? ApiKey { get; init; }

    public string? ApiKeyEnvironmentVariable { get; init; }

    public int TimeoutSeconds { get; init; } = 60;

    public string Notes { get; init; } = string.Empty;
}

public sealed class AiPromptOptions
{
    public IReadOnlyList<string> SystemPromptLines { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> UserPromptTemplateLines { get; init; } = Array.Empty<string>();
}
