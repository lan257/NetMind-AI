using NetMind.Common.Logging;
using NetMind.Models.Dtos;
using NetMind.Repository.Implementations;
using NetMind.Services.Configurations;
using NetMind.Services.Implementations;

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
            }
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
    NullAppLogger.Instance);

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
var nodeService = new NodeService(new NodeRepository(connectionFactory, NullAppLogger.Instance));
var relationService = new NodeRelationService(new NodeRelationRepository(connectionFactory, NullAppLogger.Instance));
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
