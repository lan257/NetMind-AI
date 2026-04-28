using NetMind.Models.Entities;

namespace NetMind.Repository.Interfaces;

public interface INodeRepository
{
    Task<IReadOnlyList<NodeEntity>> ListByMapAsync(long mapId);

    Task<NodeEntity?> GetAsync(long id);

    Task<NodeEntity> CreateAsync(long mapId, long? parentId, string title, string? content, int orderNo);

    Task<NodeEntity?> UpdateAsync(long id, long? parentId, string title, string? content, int orderNo);

    Task<int> DeleteSelfAsync(long id);

    Task<int> DeleteSubtreeAsync(long id);
}
