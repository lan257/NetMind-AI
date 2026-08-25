namespace NetMind.Models.Dtos;

/// <summary>
/// 知识探索结果：以中心节点为起点的 depth 层关联子图。
/// </summary>
public sealed class ExploreNodeResultDto
{
    public NodeDto CenterNode { get; init; }

    public List<NodeDto> Nodes { get; init; } = new();          // 含中心节点，按 id 去重

    public List<NodeRelationDto> Relations { get; init; } = new(); // 子图内关系，按 id 去重
}