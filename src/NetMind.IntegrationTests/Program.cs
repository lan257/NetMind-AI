using NetMind.Models.Dtos;
using NetMind.Repository.Implementations;
using NetMind.Services.Implementations;

var store = new InMemoryMindMapStore();
var mindMapService = new MindMapService(new MindMapRepository(store));
var nodeService = new NodeService(new NodeRepository(store));
var relationService = new NodeRelationService(new NodeRelationRepository(store));

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

Console.WriteLine("NetMind integration tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
