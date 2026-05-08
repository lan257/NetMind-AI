using NetMind.Models.Entities;

namespace NetMind.Repository.Interfaces;

public interface INodeRelationRepository
{
    Task<IReadOnlyList<NodeRelationEntity>> ListByMapAsync(long mapId);

    Task<IReadOnlyList<NodeRelationEntity>> ListBySourceAsync(long sourceId);

    Task<IReadOnlyList<NodeRelationEntity>> ListByNodeAsync(long nodeId);

    Task<NodeRelationEntity?> GetAsync(long id);

    Task<NodeRelationEntity> CreateAsync(long sourceId, long targetId, string relationType, double weight, long mapId);

    Task<NodeRelationEntity?> UpdateAsync(long id, string relationType, double weight);

    Task<int> DeleteAsync(long id);

    Task<int> DeleteByNodeAsync(long nodeId);
}
