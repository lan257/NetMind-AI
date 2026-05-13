namespace NetMind.Services.Configurations;

public sealed class AiAgentOptions
{
    public string AgentBuildPath { get; init; } = @"G:\AAW+\NetMind\AgentBuild";

    public string PythonExecutable { get; init; } = "py";

    public int TimeoutSeconds { get; init; } = 120;

    public double Temperature { get; init; } = 0.2;

    public int MaxTokens { get; init; } = 4096;

    public int MaxRetries { get; init; } = 2;

    public AiAgentScenarioOptions NodeQuestion { get; init; } = new();
}

public sealed class AiAgentScenarioOptions
{
    public string DomainAndSkillBinding { get; init; } = "default";

    public IReadOnlyList<string> IdentityLines { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CuesLines { get; init; } = Array.Empty<string>();
}
