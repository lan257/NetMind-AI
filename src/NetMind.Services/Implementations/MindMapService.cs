using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed class MindMapService : IMindMapService
{
    private readonly IMindMapRepository _repository;

    public MindMapService(IMindMapRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<MindMapDto>> ListAsync()
    {
        return (await _repository.ListAsync()).Select(ToDto).ToList();
    }

    public async Task<MindMapDto?> GetAsync(long id)
    {
        var entity = await _repository.GetAsync(id);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<MindMapDto> CreateAsync(CreateMindMapRequest request)
    {
        var title = RequireText(request.Title, nameof(request.Title));
        return ToDto(await _repository.CreateAsync(title));
    }

    public async Task<MindMapDto?> UpdateAsync(long id, UpdateMindMapRequest request)
    {
        var title = RequireText(request.Title, nameof(request.Title));
        var entity = await _repository.UpdateAsync(id, title, request.RootNodeId);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<DeleteResultDto> DeleteAsync(long id, bool cascade)
    {
        var affected = await _repository.DeleteAsync(id, cascade);
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

    private static MindMapDto ToDto(MindMapEntity entity)
    {
        return new MindMapDto
        {
            Id = entity.Id,
            Title = entity.Title,
            RootNodeId = entity.RootNodeId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
