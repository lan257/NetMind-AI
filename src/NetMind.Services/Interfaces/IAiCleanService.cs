using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface IAiCleanService
{
    IReadOnlyList<AiModelOptionDto> ListModels();

    Task<AiCleanResultDto> CleanAsync(AiCleanRequest request);

    Task<AiRequirementStructureResultDto> StructureRequirementAsync(AiRequirementStructureRequest request);

    Task<AiContextChatResultDto> ChatWithContextAsync(AiContextChatRequest request);

    Task<AiNodeChatResult> ChatWithNodeAsync(AiNodeChatRequest request);

    Task<AiMapChatResult> ChatWithMapAsync(AiMapChatRequest request);

    Task<AiAppHelpResult> ChatForAppHelpAsync(AiAppHelpRequest request);
}