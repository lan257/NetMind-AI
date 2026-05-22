using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface INodeRelationService
{
    Task<IReadOnlyList<NodeRelationDto>> ListByMapAsync(long mapId);

    Task<IReadOnlyList<NodeRelationDto>> ListByNodeAsync(long nodeId);

    Task<NodeRelationDto?> GetAsync(long id);

    Task<NodeRelationDto> CreateAsync(CreateNodeRelationRequest request);

    Task<NodeRelationDto?> UpdateAsync(long id, UpdateNodeRelationRequest request);

    Task<DeleteResultDto> DeleteAsync(long id);

    Task<DeleteResultDto> DeleteByNodeAsync(long nodeId);
}
