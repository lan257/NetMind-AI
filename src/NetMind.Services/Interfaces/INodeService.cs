using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface INodeService
{
    Task<IReadOnlyList<NodeDto>> ListByMapAsync(long mapId);

    Task<NodeDto?> GetAsync(long id);

    Task<NodeDto> CreateAsync(CreateNodeRequest request);

    Task<NodeDto?> UpdateAsync(long id, UpdateNodeRequest request);

    Task<DeleteResultDto> DeleteSelfAsync(long id);

    Task<DeleteResultDto> DeleteSubtreeAsync(long id);
}
