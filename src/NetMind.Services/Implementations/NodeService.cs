using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed class NodeService : INodeService
{
    private readonly INodeRepository _repository;

    public NodeService(INodeRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<NodeDto>> ListByMapAsync(long mapId)
    {
        return (await _repository.ListByMapAsync(mapId)).Select(ToDto).ToList();
    }

    public async Task<NodeDto?> GetAsync(long id)
    {
        var entity = await _repository.GetAsync(id);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<NodeDto> CreateAsync(CreateNodeRequest request)
    {
        var title = RequireText(request.Title, nameof(request.Title));
        return ToDto(await _repository.CreateAsync(request.MapId, request.ParentId, title, request.Content, request.OrderNo));
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

        var entity = await _repository.UpdateAsync(id, request.ParentId, title, request.Content, request.OrderNo);
        return entity is null ? null : ToDto(entity);
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
            ParentId = entity.ParentId,
            Title = entity.Title,
            Content = entity.Content,
            OrderNo = entity.OrderNo,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
