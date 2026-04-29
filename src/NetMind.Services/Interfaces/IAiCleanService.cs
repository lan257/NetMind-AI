using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface IAiCleanService
{
    IReadOnlyList<AiModelOptionDto> ListModels();

    Task<AiCleanResultDto> CleanAsync(AiCleanRequest request);
}
