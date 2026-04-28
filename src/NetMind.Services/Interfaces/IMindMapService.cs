using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface IMindMapService
{
    Task<IReadOnlyList<MindMapDto>> ListAsync();

    Task<MindMapDto?> GetAsync(long id);

    Task<MindMapDto> CreateAsync(CreateMindMapRequest request);

    Task<MindMapDto?> UpdateAsync(long id, UpdateMindMapRequest request);

    Task<DeleteResultDto> DeleteAsync(long id, bool cascade);
}
