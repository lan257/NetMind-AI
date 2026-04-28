using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed class NodeRelationService : INodeRelationService
{
    private readonly INodeRelationRepository _repository;

    public NodeRelationService(INodeRelationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<NodeRelationDto>> ListByMapAsync(long mapId)
    {
        return (await _repository.ListByMapAsync(mapId)).Select(ToDto).ToList();
    }

    public async Task<NodeRelationDto?> GetAsync(long id)
    {
        var entity = await _repository.GetAsync(id);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<NodeRelationDto> CreateAsync(CreateNodeRelationRequest request)
    {
        var relationType = RequireText(request.RelationType, nameof(request.RelationType));
        if (request.Weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Weight), "Weight must be greater than or equal to 0.");
        }

        return ToDto(await _repository.CreateAsync(request.SourceId, request.TargetId, relationType, request.Weight, request.MapId));
    }

    public async Task<NodeRelationDto?> UpdateAsync(long id, UpdateNodeRelationRequest request)
    {
        var relationType = RequireText(request.RelationType, nameof(request.RelationType));
        if (request.Weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Weight), "Weight must be greater than or equal to 0.");
        }

        var entity = await _repository.UpdateAsync(id, relationType, request.Weight);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<DeleteResultDto> DeleteAsync(long id)
    {
        var affected = await _repository.DeleteAsync(id);
        return new DeleteResultDto { Deleted = affected > 0, AffectedCount = affected };
    }

    public async Task<DeleteResultDto> DeleteByNodeAsync(long nodeId)
    {
        var affected = await _repository.DeleteByNodeAsync(nodeId);
        return new DeleteResultDto { Deleted = affected > 0, AffectedCount = affected };
    }

    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        return value.Trim();
    }

    private static NodeRelationDto ToDto(NodeRelationEntity entity)
    {
        return new NodeRelationDto
        {
            Id = entity.Id,
            SourceId = entity.SourceId,
            TargetId = entity.TargetId,
            RelationType = entity.RelationType,
            Weight = entity.Weight,
            MapId = entity.MapId,
            CreatedAt = entity.CreatedAt
        };
    }
}
