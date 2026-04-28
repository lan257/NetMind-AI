using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;

namespace NetMind.Repository.Implementations;

public sealed class MindMapRepository : IMindMapRepository
{
    private readonly InMemoryMindMapStore _store;

    public MindMapRepository(InMemoryMindMapStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<MindMapEntity>> ListAsync()
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<MindMapEntity>>(
                _store.MindMaps
                    .Where(map => !map.IsDeleted)
                    .OrderBy(map => map.Id)
                    .Select(Clone)
                    .ToList());
        }
    }

    public Task<MindMapEntity?> GetAsync(long id)
    {
        lock (_store.SyncRoot)
        {
            var map = _store.MindMaps.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            return Task.FromResult(map is null ? null : Clone(map));
        }
    }

    public Task<MindMapEntity> CreateAsync(string title)
    {
        lock (_store.SyncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var entity = new MindMapEntity
            {
                Id = _store.NextMapId(),
                Title = title.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            _store.MindMaps.Add(entity);
            return Task.FromResult(Clone(entity));
        }
    }

    public Task<MindMapEntity?> UpdateAsync(long id, string title, long? rootNodeId)
    {
        lock (_store.SyncRoot)
        {
            var map = _store.MindMaps.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            if (map is null)
            {
                return Task.FromResult<MindMapEntity?>(null);
            }

            if (rootNodeId.HasValue && !_store.Nodes.Any(node => node.Id == rootNodeId.Value && node.MapId == id && !node.IsDeleted))
            {
                return Task.FromResult<MindMapEntity?>(null);
            }

            map.Title = title.Trim();
            map.RootNodeId = rootNodeId;
            map.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.FromResult<MindMapEntity?>(Clone(map));
        }
    }

    public Task<int> DeleteAsync(long id, bool cascade)
    {
        lock (_store.SyncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var affected = 0;
            var map = _store.MindMaps.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            if (map is null)
            {
                return Task.FromResult(0);
            }

            MarkDeleted(map, now);
            affected++;

            if (cascade)
            {
                var nodeIds = _store.Nodes
                    .Where(node => node.MapId == id && !node.IsDeleted)
                    .Select(node => node.Id)
                    .ToHashSet();

                foreach (var node in _store.Nodes.Where(node => nodeIds.Contains(node.Id) && !node.IsDeleted))
                {
                    MarkDeleted(node, now);
                    affected++;
                }

                foreach (var relation in _store.Relations.Where(relation => relation.MapId == id && !relation.IsDeleted))
                {
                    MarkDeleted(relation, now);
                    affected++;
                }
            }

            return Task.FromResult(affected);
        }
    }

    private static void MarkDeleted(MindMapEntity entity, DateTimeOffset now)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
    }

    private static void MarkDeleted(NodeEntity entity, DateTimeOffset now)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
    }

    private static void MarkDeleted(NodeRelationEntity entity, DateTimeOffset now)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = now;
    }

    private static MindMapEntity Clone(MindMapEntity entity)
    {
        return new MindMapEntity
        {
            Id = entity.Id,
            Title = entity.Title,
            RootNodeId = entity.RootNodeId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt
        };
    }
}
