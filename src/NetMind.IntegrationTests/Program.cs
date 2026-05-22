using NetMind.Common.Logging;
using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Implementations;
using NetMind.Repository.Interfaces;
using NetMind.Services.Configurations;
using NetMind.Services.Implementations;
using System.Reflection;
using System.Text.Json;

// Stub repositories for AI configuration tests (no database needed)
var stubNodeRepo = new StubNodeRepository();
var stubRelationRepo = new StubNodeRelationRepository();

var aiCleanService = new AiCleanService(
    new AiCleanOptions
    {
        Prompt = new AiPromptOptions
        {
            ContextCompressionThreshold = 100,
            SystemPromptLines = new[]
            {
                "Return strict JSON only.",
                "Do not wrap the response in markdown."
            },
            UserPromptTemplateLines = new[]
            {
                "Convert the user text into {{schemaVersion}}.",
                "User text:",
                "{{naturalLanguage}}"
            },
            RequirementPromptTemplateLines = new[]
            {
                "Structure requirement into {{schemaVersion}}.",
                "Context:",
                "{{context}}",
                "Requirement:",
                "{{requirement}}"
            },
            ContextChatPromptTemplateLines = new[]
            {
                "Return { \"reply\": \"...\" }.",
                "Context:",
                "{{context}}",
                "Message:",
                "{{message}}"
            },
            ContextCompressionPromptTemplateLines = new[]
            {
                "Compress context:",
                "{{context}}"
            },
            NodeChatPromptTemplateLines = new[] { "Node chat prompt." },
            NodeChatCompressionPromptTemplateLines = new[] { "Node chat compression prompt." },
            MapChatPromptTemplateLines = new[] { "Map chat prompt." },
            AppHelpPromptTemplateLines = new[] { "App help prompt." },
            AppManualLines = new[] { "App manual content." }
        },
        Models = new[]
        {
            new AiModelOptions
            {
                Id = "deepseek-cloud",
                Name = "DeepSeek Cloud",
                Provider = "deepseek",
                Endpoint = "https://api.deepseek.com/chat/completions",
                Model = "deepseek-chat",
                Enabled = true,
                IsDefault = true,
                ApiKeyEnvironmentVariable = "DEEPSEEK_API_KEY"
            },
            new AiModelOptions
            {
                Id = "ollama-local",
                Name = "Ollama Local",
                Provider = "ollama",
                Endpoint = "http://127.0.0.1:11434/api/chat",
                Model = "deepseek-r1:7b",
                Enabled = true
            }
        }
    },
    new HttpClient(),
    NullAppLogger.Instance,
    stubNodeRepo,
    stubRelationRepo);

var aiModels = aiCleanService.ListModels();
Assert(aiModels.Count == 2, "AI model list should be read from configuration.");
Assert(aiModels[0].Id == "deepseek-cloud" && aiModels[0].IsDefault, "Cloud DeepSeek should be the default AI cleaner.");
Assert(aiModels.Any(model => model.Id == "ollama-local"), "Local Ollama fallback should be configured.");

var aiAgentService = new AiAgentService(
    new AiAgentOptions
    {
        NetMindApiBaseUrl = "http://127.0.0.1:5120/",
        SkillRuntimeTimeoutSeconds = 9
    },
    new AiCleanOptions
    {
        Prompt = new AiPromptOptions
        {
            AppManualPath = @"C:\NetMind\Config\AiCleanPrompts\directions-help.prompt.md",
            AppHelpLearningPath = @"C:\NetMind\Config\AiCleanPrompts\app-help-learning-log.md",
            AppHelpUsageTipsPath = @"C:\NetMind\Config\AiCleanPrompts\app-help-usage-tips.md"
        }
    },
    null!,
    stubNodeRepo,
    stubRelationRepo,
    NullAppLogger.Instance);
AssertKernelV2RequestContract(aiAgentService);
AssertAppHelpFocusContext(aiAgentService);

var connectionString = Environment.GetEnvironmentVariable("NETMIND_TEST_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("NETMIND_TEST_POSTGRES_CONNECTION is not set; database integration tests were skipped.");
    Console.WriteLine("NetMind integration tests passed.");
    return;
}

var connectionFactory = new PostgresConnectionFactory(connectionString);
var mindMapService = new MindMapService(new MindMapRepository(connectionFactory, NullAppLogger.Instance));
var nodeRelationRepository = new NodeRelationRepository(connectionFactory, NullAppLogger.Instance);
var nodeService = new NodeService(new NodeRepository(connectionFactory, NullAppLogger.Instance), nodeRelationRepository);
var relationService = new NodeRelationService(nodeRelationRepository);
var transferService = new MindMapTransferService(mindMapService, nodeService, relationService);

var createdMap = await mindMapService.CreateAsync(new CreateMindMapRequest { Title = "P1.2 集成测试导图" });
Assert(createdMap.Id > 0, "Mind map should be created in PostgreSQL.");

var root = await nodeService.CreateAsync(new CreateNodeRequest { MapId = createdMap.Id, Title = "根节点", OrderNo = 1 });
var child = await nodeService.CreateAsync(new CreateNodeRequest { MapId = createdMap.Id, ParentId = root.Id, Title = "子节点", OrderNo = 1 });
Assert((await nodeService.ListByMapAsync(createdMap.Id)).Count == 2, "Nodes should be created and listed from PostgreSQL.");

var relation = await relationService.CreateAsync(new CreateNodeRelationRequest
{
    MapId = createdMap.Id,
    SourceId = root.Id,
    TargetId = child.Id,
    RelationType = "relates_to",
    Weight = 1
});
Assert(relation.Id > 0, "Node relation should be created in PostgreSQL.");

var exported = await transferService.ExportAsync(createdMap.Id);
Assert(exported is not null && exported.Transfer.Nodes.Count == 2 && exported.Transfer.Relations.Count == 1, "Export should read complete PostgreSQL data.");

var deleteResult = await mindMapService.DeleteAsync(createdMap.Id, cascade: true);
Assert(deleteResult.AffectedCount == 4, "Cascade delete should mark the map, nodes and relation as deleted.");

Console.WriteLine("NetMind integration tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertKernelV2RequestContract(AiAgentService service)
{
    var buildRequest = typeof(AiAgentService).GetMethod(
        "BuildKernelRequest",
        BindingFlags.Instance | BindingFlags.NonPublic);
    Assert(buildRequest is not null, "Agent kernel request builder should be available for contract checks.");

    var agentContext = new Dictionary<string, object?>
    {
        ["focus_context"] = new Dictionary<string, object?>()
    };
    var request = new AiAgentChatRequest
    {
        ConversationId = "contract-conversation",
        Message = "继续执行",
        Domain = "netmind",
        ConfirmedToolCalls = new[] { Json("{\"call_id\":\"tool-call\",\"approved\":false,\"denied_reason\":\"no\"}") },
        HistoryToolCalls = new[] { Json("{\"call_id\":\"tool-history\",\"tool_id\":\"node_get\"}") },
        ConfirmedSkillCalls = new[] { Json("{\"call_id\":\"legacy-call\",\"approved\":true}") },
        HistorySkillCalls = new[] { Json("{\"call_id\":\"legacy-history\",\"skill_id\":\"legacy\"}") }
    };
    var scenario = new AiAgentScenarioOptions
    {
        DomainAndSkillBinding = "scenario-domain",
        IdentityLines = new[] { "contract identity" },
        CuesLines = new[] { "contract cues" }
    };
    var modelConfig = new Dictionary<string, object?> { ["model_name"] = "fake" };

    var rawResult = buildRequest!.Invoke(service, new object[]
    {
        request,
        scenario,
        modelConfig,
        agentContext,
        "contract-agent"
    });
    Assert(rawResult is Dictionary<string, object?>, "Agent kernel request builder should return a payload dictionary.");

    var payload = (Dictionary<string, object?>)rawResult!;
    Assert(payload["api_version"] as string == "v2", "Agent kernel requests should opt into API v2.");
    Assert(payload["domain"] as string == "netmind", "Agent kernel requests should send the v2 domain field.");
    Assert(!payload.ContainsKey("domain_and_skill_binding"), "Agent kernel requests should not send the v1 domain alias.");
    Assert(payload.ContainsKey("tool_runtime"), "Agent kernel requests should send tool_runtime.");
    Assert(!payload.ContainsKey("skill_runtime"), "Agent kernel requests should not send skill_runtime.");
    Assert(payload.ContainsKey("confirmed_tool_calls"), "Agent kernel requests should send confirmed_tool_calls.");
    Assert(!payload.ContainsKey("confirmed_skill_calls"), "Agent kernel requests should not send confirmed_skill_calls.");
    Assert(payload.ContainsKey("history_tool_calls"), "Agent kernel requests should send history_tool_calls.");
    Assert(!payload.ContainsKey("history_skill_calls"), "Agent kernel requests should not send history_skill_calls.");

    var runtime = payload["tool_runtime"] as Dictionary<string, object?>;
    Assert(runtime is not null, "tool_runtime should be an object.");
    var sharedRuntime = runtime!["shared"] as Dictionary<string, object?>;
    Assert(sharedRuntime is not null, "tool_runtime.shared should be an object.");
    Assert(sharedRuntime!["netmind_api_base_url"] as string == "http://127.0.0.1:5120", "Tool runtime should normalize NetMind API base URL.");
    Assert((int)sharedRuntime["timeout_seconds"]! == 9, "Tool runtime should carry timeout into shared runtime.");

    var confirmedCalls = payload["confirmed_tool_calls"] as IReadOnlyList<object?>;
    Assert(confirmedCalls?.Count == 1, "Tool confirmations should prefer the v2 request field.");
    var confirmedCall = confirmedCalls![0] as Dictionary<string, object?>;
    Assert(confirmedCall is not null && confirmedCall.ContainsKey("reject_reason"), "Denied tool confirmations should normalize reject_reason.");

    var historyCalls = payload["history_tool_calls"] as IReadOnlyList<JsonElement>;
    Assert(historyCalls?.Count == 1 && historyCalls[0].GetProperty("call_id").GetString() == "tool-history", "Tool history should prefer the v2 request field.");

    var focusContext = agentContext["focus_context"] as Dictionary<string, object?>;
    Assert(focusContext?["domain"] as string == "netmind", "Agent focus context should expose the v2 domain name.");
}

static void AssertAppHelpFocusContext(AiAgentService service)
{
    var buildFocusContext = typeof(AiAgentService).GetMethod(
        "BuildAppHelpFocusContext",
        BindingFlags.Instance | BindingFlags.NonPublic);
    Assert(buildFocusContext is not null, "App help focus context builder should be available for contract checks.");

    var rawResult = buildFocusContext!.Invoke(service, new object[]
    {
        "history",
        2048,
        10.0,
        "healthy"
    });
    Assert(rawResult is Dictionary<string, object?>, "App help focus context should be a dictionary.");

    var focusContext = (Dictionary<string, object?>)rawResult!;
    Assert(
        focusContext["usage_tips_absolute_path"] as string == @"C:\NetMind\Config\AiCleanPrompts\app-help-usage-tips.md",
        "App help focus context should expose the usage tips path.");
    Assert(
        (focusContext["usage_tips_update_policy"] as string)?.Contains("incremental_file_modifier") == true,
        "App help usage tips policy should name the incremental modifier tool.");
}

static JsonElement Json(string text)
{
    return JsonSerializer.Deserialize<JsonElement>(text);
}

internal sealed class StubNodeRepository : INodeRepository
{
    public Task<IReadOnlyList<NodeEntity>> ListByMapAsync(long mapId) => Task.FromResult<IReadOnlyList<NodeEntity>>(Array.Empty<NodeEntity>());
    public Task<IReadOnlyList<NodeEntity>> SearchAsync(long? mapId, string keyword, int limit) => Task.FromResult<IReadOnlyList<NodeEntity>>(Array.Empty<NodeEntity>());
    public Task<NodeEntity?> GetAsync(long id) => Task.FromResult<NodeEntity?>(null);
    public Task<bool> ExistsSiblingOrderNoAsync(long mapId, long? parentId, int orderNo, long excludeNodeId) => Task.FromResult(false);
    public Task<NodeEntity> CreateAsync(long mapId, long? parentId, string title, string? content, int orderNo, double? positionX, double? positionY) => Task.FromResult(new NodeEntity());
    public Task<NodeEntity?> UpdateAsync(long id, long? parentId, string title, string? content, int orderNo, double? positionX, double? positionY) => Task.FromResult<NodeEntity?>(null);
    public Task<int> DeleteSelfAsync(long id) => Task.FromResult(0);
    public Task<int> DeleteSubtreeAsync(long id) => Task.FromResult(0);
}

internal sealed class StubNodeRelationRepository : INodeRelationRepository
{
    public Task<IReadOnlyList<NodeRelationEntity>> ListByMapAsync(long mapId) => Task.FromResult<IReadOnlyList<NodeRelationEntity>>(Array.Empty<NodeRelationEntity>());
    public Task<IReadOnlyList<NodeRelationEntity>> ListBySourceAsync(long sourceId) => Task.FromResult<IReadOnlyList<NodeRelationEntity>>(Array.Empty<NodeRelationEntity>());
    public Task<IReadOnlyList<NodeRelationEntity>> ListByNodeAsync(long nodeId) => Task.FromResult<IReadOnlyList<NodeRelationEntity>>(Array.Empty<NodeRelationEntity>());
    public Task<NodeRelationEntity?> GetAsync(long id) => Task.FromResult<NodeRelationEntity?>(null);
    public Task<NodeRelationEntity> CreateAsync(long sourceId, long targetId, string relationType, double weight, long mapId) => Task.FromResult(new NodeRelationEntity());
    public Task<NodeRelationEntity?> UpdateAsync(long id, string relationType, double weight) => Task.FromResult<NodeRelationEntity?>(null);
    public Task<int> DeleteAsync(long id) => Task.FromResult(0);
    public Task<int> DeleteByNodeAsync(long nodeId) => Task.FromResult(0);
}
