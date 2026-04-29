using NetMind.Models.Dtos;
using NetMind.Repository.Implementations;
using NetMind.Services.Implementations;

var store = new InMemoryMindMapStore();
var mindMapService = new MindMapService(new MindMapRepository(store));
var nodeService = new NodeService(new NodeRepository(store));
var relationService = new NodeRelationService(new NodeRelationRepository(store));
var transferService = new MindMapTransferService(mindMapService, nodeService, relationService);
var aiCleanService = new AiCleanService();

var maps = await mindMapService.ListAsync();
Assert(maps.Count >= 3, "Seed mind maps should be queryable.");

var createdMap = await mindMapService.CreateAsync(new CreateMindMapRequest { Title = "集成测试导图" });
Assert(createdMap.Id > 0, "Mind map should be created.");

var updatedMap = await mindMapService.UpdateAsync(createdMap.Id, new UpdateMindMapRequest { Title = "集成测试导图-已更新" });
Assert(updatedMap?.Title == "集成测试导图-已更新", "Mind map should be updated.");

var root = await nodeService.CreateAsync(new CreateNodeRequest { MapId = createdMap.Id, Title = "根节点", OrderNo = 1 });
var child = await nodeService.CreateAsync(new CreateNodeRequest { MapId = createdMap.Id, ParentId = root.Id, Title = "子节点", OrderNo = 1 });
var grandChild = await nodeService.CreateAsync(new CreateNodeRequest { MapId = createdMap.Id, ParentId = child.Id, Title = "孙节点", OrderNo = 1 });
Assert((await nodeService.ListByMapAsync(createdMap.Id)).Count == 3, "Nodes should be created and listed.");

var relation = await relationService.CreateAsync(new CreateNodeRelationRequest
{
    MapId = createdMap.Id,
    SourceId = root.Id,
    TargetId = child.Id,
    RelationType = "relates_to",
    Weight = 1
});
Assert(relation.Id > 0, "Node relation should be created.");

var relationDeleteResult = await relationService.DeleteByNodeAsync(child.Id);
Assert(relationDeleteResult.AffectedCount == 1, "Relations connected to a node should be logically deleted.");

var deleteSelfResult = await nodeService.DeleteSelfAsync(child.Id);
Assert(deleteSelfResult.AffectedCount == 1, "Deleting node self should affect only itself.");
var promotedGrandChild = await nodeService.GetAsync(grandChild.Id);
Assert(promotedGrandChild is not null && promotedGrandChild.ParentId == root.Id, "Deleting node self should keep children and promote them.");

var subtreeResult = await nodeService.DeleteSubtreeAsync(root.Id);
Assert(subtreeResult.AffectedCount == 2, "Deleting subtree should affect root and remaining descendant.");

var cascadeMap = await mindMapService.CreateAsync(new CreateMindMapRequest { Title = "级联删除导图" });
var cascadeRoot = await nodeService.CreateAsync(new CreateNodeRequest { MapId = cascadeMap.Id, Title = "级联根节点", OrderNo = 1 });
var cascadeChild = await nodeService.CreateAsync(new CreateNodeRequest { MapId = cascadeMap.Id, ParentId = cascadeRoot.Id, Title = "级联子节点", OrderNo = 1 });
await relationService.CreateAsync(new CreateNodeRelationRequest
{
    MapId = cascadeMap.Id,
    SourceId = cascadeRoot.Id,
    TargetId = cascadeChild.Id,
    RelationType = "depends_on",
    Weight = 0.5
});
var cascadeDelete = await mindMapService.DeleteAsync(cascadeMap.Id, cascade: true);
Assert(cascadeDelete.AffectedCount == 4, "Cascade deleting a mind map should delete the map, its nodes and relations.");

var plainDelete = await mindMapService.DeleteAsync(createdMap.Id, cascade: false);
Assert(plainDelete.AffectedCount == 1, "Deleting a mind map without cascade should affect only the map.");

var template = transferService.CreateTemplate();
Assert(template.SchemaVersion == "netmind.mindmap.v1" && template.Nodes.Count == 2, "Import template should use the stable transfer schema.");

var importRequest = new ImportMindMapRequest
{
    MindMap = new MindMapTransferDto
    {
        SchemaVersion = "netmind.mindmap.v1",
        Title = "P1 import map",
        Nodes = new[]
        {
            new MindMapTransferNodeDto { ClientId = "root", Title = "Imported root", Content = "Root content", OrderNo = 1 },
            new MindMapTransferNodeDto { ClientId = "child", ParentClientId = "root", Title = "Imported child", Content = "Child content", OrderNo = 1 }
        },
        Relations = new[]
        {
            new MindMapTransferRelationDto { SourceClientId = "root", TargetClientId = "child", RelationType = "supports", Weight = 0.6 }
        }
    }
};

var imported = await transferService.ImportAsync(importRequest);
Assert(imported.Structure.Map.Title == "P1 import map", "Structured import should create a mind map.");
Assert(imported.Structure.Nodes.Count == 2, "Structured import should create all nodes.");
Assert(imported.Structure.Relations.Count == 1, "Structured import should create all relations.");
Assert(imported.NodeIdMap.ContainsKey("root") && imported.NodeIdMap.ContainsKey("child"), "Structured import should return client id mapping.");

var exported = await transferService.ExportAsync(imported.Structure.Map.Id);
Assert(exported is not null, "Exported structure should be returned.");
var exportedStructure = exported ?? throw new InvalidOperationException("Exported structure should be returned.");
Assert(exportedStructure.Transfer.Nodes.Count == 2 && exportedStructure.Transfer.Relations.Count == 1, "Exported transfer should include nodes and relations.");
Assert(exportedStructure.Transfer.Nodes.Any(node => node.ParentClientId is not null), "Exported transfer should preserve hierarchy through client ids.");

try
{
    await transferService.ImportAsync(new ImportMindMapRequest
    {
        MindMap = new MindMapTransferDto
        {
            SchemaVersion = "netmind.mindmap.v1",
            Title = "Invalid relation map",
            Nodes = new[] { new MindMapTransferNodeDto { ClientId = "root", Title = "Root", OrderNo = 1 } },
            Relations = new[] { new MindMapTransferRelationDto { SourceClientId = "root", TargetClientId = "missing", RelationType = "relates_to", Weight = 1 } }
        }
    });
    throw new InvalidOperationException("Invalid relation import should fail.");
}
catch (ArgumentException)
{
    // Expected validation failure.
}

var aiModels = aiCleanService.ListModels();
Assert(aiModels.Count >= 2, "AI model placeholders should be listed.");
Assert(aiModels[0].IsDefault && aiModels[0].Id == "local-deepseek-placeholder", "The first AI model should be the default placeholder.");

var cleanResult = aiCleanService.Clean(new AiCleanRequest
{
    NaturalLanguage = """
        Product planning knowledge map
        - Requirements collection
        - User confirmation
        - Structured import
        """,
    ModelId = aiModels[0].Id
});
Assert(cleanResult.SelectedModel.Id == aiModels[0].Id, "AI clean should use the requested placeholder model.");
Assert(cleanResult.Transfer.SchemaVersion == "netmind.mindmap.v1", "AI clean should output the standard transfer schema.");
Assert(cleanResult.Transfer.Nodes.Count >= 4, "AI clean should expand natural language into nodes.");
Assert(cleanResult.Transfer.Relations.Count >= 3, "AI clean should create root expansion relations.");

var importedClean = await transferService.ImportAsync(new ImportMindMapRequest { MindMap = cleanResult.Transfer });
Assert(importedClean.Structure.Nodes.Count == cleanResult.Transfer.Nodes.Count, "AI cleaned structure should be importable.");

Console.WriteLine("NetMind integration tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
