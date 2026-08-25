using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface IExploreNodeService
{
    /// <summary>
    /// 从指定节点出发，按 depth（1~3）做真实 BFS 批量查询，返回关联子图。
    /// 节点不存在时返回 null；depth 越界抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </summary>
    Task<ExploreNodeResultDto?> ExploreAsync(long nodeId, int depth);
}