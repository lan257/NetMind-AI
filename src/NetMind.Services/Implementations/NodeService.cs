using System.Text.RegularExpressions;
using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed partial class NodeService : INodeService
{
    private readonly INodeRepository _repository;
    private readonly INodeRelationRepository _relationRepository;

    [GeneratedRegex(@"\[\[.*?\|(\d+)\]\]")]
    private static partial Regex ReferenceRegex();

    public NodeService(INodeRepository repository, INodeRelationRepository relationRepository)
    {
        _repository = repository;
        _relationRepository = relationRepository;
    }

    public async Task<IReadOnlyList<NodeDto>> ListByMapAsync(long mapId)
    {
        return (await _repository.ListByMapAsync(mapId)).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<NodeDto>> SearchAsync(long? mapId, string keyword, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<NodeDto>();
        }

        return (await _repository.SearchAsync(mapId, keyword.Trim(), limit)).Select(ToDto).ToList();
    }

    public async Task<NodeDto?> GetAsync(long id)
    {
        var entity = await _repository.GetAsync(id);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<NodeDto> CreateAsync(CreateNodeRequest request)
    {
        var title = RequireText(request.Title, nameof(request.Title));
        return ToDto(await _repository.CreateAsync(
            request.MapId,
            request.ParentId,
            title,
            request.Content,
            request.OrderNo,
            request.PositionX,
            request.PositionY));
    }

    public async Task<NodeDto?> UpdateAsync(long id, UpdateNodeRequest request)
    {
        var title = RequireText(request.Title, nameof(request.Title));
        var current = await _repository.GetAsync(id);
        if (current is null)
        {
            return null;
        }

        if (await _repository.ExistsSiblingOrderNoAsync(current.MapId, request.ParentId, request.OrderNo, id))
        {
            throw new InvalidOperationException("同级节点排序不能重复。");
        }

        var entity = await _repository.UpdateAsync(
            id,
            request.ParentId,
            title,
            request.Content,
            request.OrderNo,
            request.PositionX,
            request.PositionY);
        if (entity != null)
        {
            await SyncRelationsAsync(entity);
        }
        return entity is null ? null : ToDto(entity);
    }

    private async Task SyncRelationsAsync(NodeEntity node)
    {
        if (string.IsNullOrWhiteSpace(node.Content))
        {
            // 如果内容为空，删除所有“引用”类型的关联
            var toCleanup = (await _relationRepository.ListBySourceAsync(node.Id))
                .Where(r => r.RelationType == "引用")
                .ToList();

            foreach (var rel in toCleanup)
                await _relationRepository.DeleteAsync(rel.Id);
            return;
        }

        var matches = ReferenceRegex().Matches(node.Content);
        var targetIds = matches
            .Select(m => long.Parse(m.Groups[1].Value))
            .Distinct()
            .Where(tid => tid != node.Id) // 不能指向自己
            .ToList();

        var existingRelations = await _relationRepository.ListBySourceAsync(node.Id);
        var existingRefRelations = existingRelations
            .Where(r => r.RelationType == "引用")
            .ToList();

        // 1. 删除不再需要的关联
        var toDelete = existingRefRelations
            .Where(r => !targetIds.Contains(r.TargetId))
            .ToList();
        foreach (var rel in toDelete)
            await _relationRepository.DeleteAsync(rel.Id);

        // 2. 新增缺失的关联
        var existingTargetIds = existingRefRelations.Select(r => r.TargetId).ToHashSet();
        var toAdd = targetIds.Where(tid => !existingTargetIds.Contains(tid)).ToList();
        foreach (var tid in toAdd)
        {
            try
            {
                await _relationRepository.CreateAsync(node.Id, tid, "引用", 1.0, node.MapId);
            }
            catch (InvalidOperationException)
            {
                // 忽略无效的目标（比如目标节点不存在）
            }
        }
    }

    public async Task<DeleteResultDto> DeleteSelfAsync(long id)
    {
        var affected = await _repository.DeleteSelfAsync(id);
        return new DeleteResultDto { Deleted = affected > 0, AffectedCount = affected };
    }

    public async Task<DeleteResultDto> DeleteSubtreeAsync(long id)
    {
        var affected = await _repository.DeleteSubtreeAsync(id);
        return new DeleteResultDto { Deleted = affected > 0, AffectedCount = affected };
    }

    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} 不能为空。", name);
        }

        return value.Trim();
    }

    private static NodeDto ToDto(NodeEntity entity)
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
}
