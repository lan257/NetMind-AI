using NetMind.Common.Logging;
using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Implementations;
using NetMind.Repository.Interfaces;
using NetMind.Services.Configurations;
using NetMind.Services.Implementations;

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
