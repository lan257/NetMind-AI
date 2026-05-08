using NetMind.Models.Entities;

namespace NetMind.Repository.Interfaces;

public interface INodeRepository
{
    Task<IReadOnlyList<NodeEntity>> ListByMapAsync(long mapId);

    Task<IReadOnlyList<NodeEntity>> SearchAsync(long? mapId, string keyword, int limit);

    Task<NodeEntity?> GetAsync(long id);

    Task<bool> ExistsSiblingOrderNoAsync(long mapId, long? parentId, int orderNo, long excludeNodeId);

    Task<NodeEntity> CreateAsync(long mapId, long? parentId, string title, string? content, int orderNo, double? positionX, double? positionY);

    Task<NodeEntity?> UpdateAsync(long id, long? parentId, string title, string? content, int orderNo, double? positionX, double? positionY);

    Task<int> DeleteSelfAsync(long id);

    Task<int> DeleteSubtreeAsync(long id);
}
