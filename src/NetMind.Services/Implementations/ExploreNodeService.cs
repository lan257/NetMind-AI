using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

/// <summary>
/// 知识探索服务：从任意节点出发按 depth 层真实 BFS 查询关联子图。
/// 关系语义：BFS 各层批量查询命中的所有边（去重后），并过滤为两端节点均在最终子图内的边；
/// 层扩展按有向边方向（source → target）发现新节点，反向边只计入关系池、不扩展。环不导致死循环。
/// </summary>
public sealed class ExploreNodeService : IExploreNodeService
{
    private readonly INodeRepository _nodeRepository;
    private readonly INodeRelationRepository _relationRepository;

    public ExploreNodeService(INodeRepository nodeRepository, INodeRelationRepository relationRepository)
    {
        _nodeRepository = nodeRepository;
        _relationRepository = relationRepository;
    }

    public async Task<ExploreNodeResultDto?> ExploreAsync(long nodeId, int depth)
    {
        if (depth is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "探索深度必须在 1~3 之间。");
        }

        var center = await _nodeRepository.GetAsync(nodeId);
        if (center is null)
        {
            return null;
        }

        // BFS：visited 防环与去重，relations 按关系 id 去重
        var visited = new HashSet<long> { nodeId };
        var relations = new Dictionary<long, NodeRelationEntity>();
        var frontier = new List<long> { nodeId };

        for (var level = 0; level < depth; level++)
        {
            // 一次查询/层，批量取当前 frontier 的全部关联（双向命中），避免 N+1
            var layerRelations = await _relationRepository.GetByNodeIdsAsync(frontier);
            var frontierSet = frontier.ToHashSet();
            var nextFrontier = new List<long>();

            foreach (var relation in layerRelations)
            {
                // 本层命中的关系全部保留（指向已访问节点的部分也保留，按 id 去重）
                relations.TryAdd(relation.Id, relation);

                // 有向扩展：frontier 中节点作为 source 时，其 target 是新节点；已访问的跳过
                if (frontierSet.Contains(relation.SourceId) && visited.Add(relation.TargetId))
                {
                    nextFrontier.Add(relation.TargetId);
                }
            }

            if (nextFrontier.Count == 0)
            {
                break;
            }

            frontier = nextFrontier;
        }

        var nodes = await _nodeRepository.GetByIdsAsync(visited);

        // 关系集 = 各层命中边的并集，过滤为两端节点均在最终子图内
        var subgraphRelations = relations.Values
            .Where(r => visited.Contains(r.SourceId) && visited.Contains(r.TargetId))
            .OrderBy(r => r.Id)
            .ToList();

        return new ExploreNodeResultDto
        {
            CenterNode = ToNodeDto(center),
            Nodes = nodes.Select(ToNodeDto).ToList(),
            Relations = subgraphRelations.Select(ToRelationDto).ToList()
        };
    }

    private static NodeDto ToNodeDto(NodeEntity entity)
    {
        return new NodeDto
        {
            Id = entity.Id,
            MapId = entity.MapId,
            MapTitle = entity.MapTitle,
            ParentId = entity.ParentId,
            Title = entity.Title,
            Content = entity.Content,
            OrderNo = entity.OrderNo,
            PositionX = entity.PositionX,
            PositionY = entity.PositionY,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static NodeRelationDto ToRelationDto(NodeRelationEntity entity)
    {
        return new NodeRelationDto
        {
            Id = entity.Id,
            SourceId = entity.SourceId,
            SourceTitle = entity.SourceTitle,
            SourceMapId = entity.SourceMapId,
            TargetId = entity.TargetId,
            TargetTitle = entity.TargetTitle,
            TargetMapId = entity.TargetMapId,
            RelationType = entity.RelationType,
            Weight = entity.Weight,
            MapId = entity.MapId,
            CreatedAt = entity.CreatedAt
        };
    }
}