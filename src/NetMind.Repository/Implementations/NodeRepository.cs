using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;

namespace NetMind.Repository.Implementations;

public sealed class NodeRepository : INodeRepository
{
    private readonly InMemoryMindMapStore _store;

    public NodeRepository(InMemoryMindMapStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<NodeEntity>> ListByMapAsync(long mapId)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<NodeEntity>>(
                _store.Nodes
                    .Where(node => node.MapId == mapId && !node.IsDeleted)
                    .OrderBy(node => node.ParentId)
                    .ThenBy(node => node.OrderNo)
                    .ThenBy(node => node.Id)
                    .Select(Clone)
                    .ToList());
        }
    }

    public Task<NodeEntity?> GetAsync(long id)
    {
        lock (_store.SyncRoot)
        {
            var node = _store.Nodes.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            return Task.FromResult(node is null ? null : Clone(node));
        }
    }

    public Task<NodeEntity> CreateAsync(long mapId, long? parentId, string title, string? content, int orderNo)
    {
        lock (_store.SyncRoot)
        {
            if (!_store.MindMaps.Any(map => map.Id == mapId && !map.IsDeleted))
            {
                throw new InvalidOperationException("Mind map does not exist.");
            }

            if (parentId.HasValue && !_store.Nodes.Any(node => node.Id == parentId.Value && node.MapId == mapId && !node.IsDeleted))
            {
                throw new InvalidOperationException("Parent node does not exist in the same mind map.");
            }

            var now = DateTimeOffset.UtcNow;
            var entity = new NodeEntity
            {
                Id = _store.NextNodeId(),
                MapId = mapId,
                ParentId = parentId,
                Title = title.Trim(),
                Content = content,
                OrderNo = orderNo,
                CreatedAt = now,
                UpdatedAt = now
            };

            _store.Nodes.Add(entity);

            var map = _store.MindMaps.First(item => item.Id == mapId);
            if (!map.RootNodeId.HasValue)
            {
                map.RootNodeId = entity.Id;
                map.UpdatedAt = now;
            }

            return Task.FromResult(Clone(entity));
        }
    }

    public Task<NodeEntity?> UpdateAsync(long id, long? parentId, string title, string? content, int orderNo)
    {
        lock (_store.SyncRoot)
        {
            var node = _store.Nodes.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            if (node is null || parentId == id)
            {
                return Task.FromResult<NodeEntity?>(null);
            }

            if (parentId.HasValue && !_store.Nodes.Any(item => item.Id == parentId.Value && item.MapId == node.MapId && !item.IsDeleted))
            {
                return Task.FromResult<NodeEntity?>(null);
            }

            node.ParentId = parentId;
            node.Title = title.Trim();
            node.Content = content;
            node.OrderNo = orderNo;
            node.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.FromResult<NodeEntity?>(Clone(node));
        }
    }

    public Task<int> DeleteSelfAsync(long id)
    {
        lock (_store.SyncRoot)
        {
            var node = _store.Nodes.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            if (node is null)
            {
                return Task.FromResult(0);
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var child in _store.Nodes.Where(item => item.ParentId == id && !item.IsDeleted))
            {
                child.ParentId = node.ParentId;
                child.UpdatedAt = now;
            }

            MarkDeleted(node, now);
            DeleteRelations(new[] { id }, now);

            var map = _store.MindMaps.FirstOrDefault(item => item.Id == node.MapId && item.RootNodeId == id);
            if (map is not null)
            {
                map.RootNodeId = _store.Nodes.FirstOrDefault(item => item.MapId == node.MapId && item.ParentId is null && !item.IsDeleted)?.Id;
                map.UpdatedAt = now;
            }

            return Task.FromResult(1);
        }
    }

    public Task<int> DeleteSubtreeAsync(long id)
    {
        lock (_store.SyncRoot)
        {
            var root = _store.Nodes.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
            if (root is null)
            {
                return Task.FromResult(0);
            }

            var now = DateTimeOffset.UtcNow;
            var subtreeIds = CollectSubtreeIds(id);
            var affected = 0;
            foreach (var node in _store.Nodes.Where(item => subtreeIds.Contains(item.Id) && !item.IsDeleted))
            {
                MarkDeleted(node, now);
                affected++;
            }

            DeleteRelations(subtreeIds, now);

            var map = _store.MindMaps.FirstOrDefault(item => item.Id == root.MapId && item.RootNodeId.HasValue && subtreeIds.Contains(item.RootNodeId.Value));
            if (map is not null)
            {
                map.RootNodeId = _store.Nodes.FirstOrDefault(item => item.MapId == root.MapId && item.ParentId is null && !item.IsDeleted)?.Id;
                map.UpdatedAt = now;
            }

            return Task.FromResult(affected);
        }
    }

    private HashSet<long> CollectSubtreeIds(long rootId)
    {
        var result = new HashSet<long> { rootId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var child in _store.Nodes.Where(node => node.ParentId.HasValue && result.Contains(node.ParentId.Value) && !node.IsDeleted))
            {
                if (result.Add(child.Id))
                {
                    changed = true;
                }
            }
        }

        return result;
    }

    private void DeleteRelations(IEnumerable<long> nodeIds, DateTimeOffset now)
    {
        var idSet = nodeIds.ToHashSet();
        foreach (var relation in _store.Relations.Where(item => !item.IsDeleted && (idSet.Contains(item.SourceId) || idSet.Contains(item.TargetId))))
        {
            relation.IsDeleted = true;
            relation.DeletedAt = now;
        }
    }

    private static void MarkDeleted(NodeEntity entity, DateTimeOffset now)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
    }

    private static NodeEntity Clone(NodeEntity entity)
    {
        return new NodeEntity
        {
            Id = entity.Id,
            MapId = entity.MapId,
            ParentId = entity.ParentId,
            Title = entity.Title,
            Content = entity.Content,
            OrderNo = entity.OrderNo,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt
        };
    }
}
