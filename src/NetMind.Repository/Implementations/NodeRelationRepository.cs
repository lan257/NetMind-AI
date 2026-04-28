using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;

namespace NetMind.Repository.Implementations;

public sealed class NodeRelationRepository : INodeRelationRepository
{
    private readonly InMemoryMindMapStore _store;

    public NodeRelationRepository(InMemoryMindMapStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<NodeRelationEntity>> ListByMapAsync(long mapId)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<NodeRelationEntity>>(
                _store.Relations
                    .Where(relation => relation.MapId == mapId && !relation.IsDeleted)
                    .OrderBy(relation => relation.Id)
                    .Select(Clone)
                    .ToList());
        }
    }

    public Task<NodeRelationEntity?> GetAsync(long id)
    {
        lock (_store.SyncRoot)
        {
            var relation = _store.Relations.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            return Task.FromResult(relation is null ? null : Clone(relation));
        }
    }

    public Task<NodeRelationEntity> CreateAsync(long sourceId, long targetId, string relationType, double weight, long mapId)
    {
        lock (_store.SyncRoot)
        {
            if (sourceId == targetId)
            {
                throw new InvalidOperationException("Source and target node cannot be the same.");
            }

            var sourceExists = _store.Nodes.Any(node => node.Id == sourceId && node.MapId == mapId && !node.IsDeleted);
            var targetExists = _store.Nodes.Any(node => node.Id == targetId && node.MapId == mapId && !node.IsDeleted);
            if (!sourceExists || !targetExists)
            {
                throw new InvalidOperationException("Source and target node must exist in the same mind map.");
            }

            var entity = new NodeRelationEntity
            {
                Id = _store.NextRelationId(),
                SourceId = sourceId,
                TargetId = targetId,
                RelationType = relationType.Trim(),
                Weight = weight,
                MapId = mapId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _store.Relations.Add(entity);
            return Task.FromResult(Clone(entity));
        }
    }

    public Task<NodeRelationEntity?> UpdateAsync(long id, string relationType, double weight)
    {
        lock (_store.SyncRoot)
        {
            var relation = _store.Relations.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            if (relation is null)
            {
                return Task.FromResult<NodeRelationEntity?>(null);
            }

            relation.RelationType = relationType.Trim();
            relation.Weight = weight;
            return Task.FromResult<NodeRelationEntity?>(Clone(relation));
        }
    }

    public Task<int> DeleteAsync(long id)
    {
        lock (_store.SyncRoot)
        {
            var relation = _store.Relations.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            if (relation is null)
            {
                return Task.FromResult(0);
            }

            MarkDeleted(relation);
            return Task.FromResult(1);
        }
    }

    public Task<int> DeleteByNodeAsync(long nodeId)
    {
        lock (_store.SyncRoot)
        {
            var affected = 0;
            foreach (var relation in _store.Relations.Where(item => !item.IsDeleted && (item.SourceId == nodeId || item.TargetId == nodeId)))
            {
                MarkDeleted(relation);
                affected++;
            }

            return Task.FromResult(affected);
        }
    }

    private static void MarkDeleted(NodeRelationEntity entity)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
    }

    private static NodeRelationEntity Clone(NodeRelationEntity entity)
    {
        return new NodeRelationEntity
        {
            Id = entity.Id,
            SourceId = entity.SourceId,
            TargetId = entity.TargetId,
            RelationType = entity.RelationType,
            Weight = entity.Weight,
            MapId = entity.MapId,
            CreatedAt = entity.CreatedAt,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt
        };
    }
}
