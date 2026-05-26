namespace NetMind.Services.Configurations;

public sealed class AiAgentOptions
{
    public string AgentBuildPath { get; init; } = "../agent";

    public string PythonExecutable { get; init; } = "py";

    public int TimeoutSeconds { get; init; } = 120;

    public double Temperature { get; init; } = 0.2;

    public int MaxTokens { get; init; } = 4096;

    public int MaxRetries { get; init; } = 2;

    public string NetMindApiBaseUrl { get; init; } = "http://127.0.0.1:5120";

    public int ToolRuntimeTimeoutSeconds { get; init; } = 10;

    public AiAgentScenarioOptions NodeQuestion { get; init; } = new();

    public AiAgentScenarioOptions MapQuestion { get; init; } = new();

    public AiAgentScenarioOptions GlobalQuestion { get; init; } = new();

    public AiAgentScenarioOptions AppHelp { get; init; } = new();
}

public sealed class AiAgentScenarioOptions
{
    public string Domain { get; init; } = "netmind";

    public IReadOnlyList<string> IdentityLines { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CuesLines { get; init; } = Array.Empty<string>();
}
