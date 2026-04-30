using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface IAiConversationRecordService
{
    Task<IReadOnlyList<AiConversationRecordDto>> ListAsync(string? conversationId);

    Task<AiConversationRecordDto?> GetAsync(long id);

    Task<AiConversationRecordDto> CreateAsync(CreateAiConversationRecordRequest request);

    Task<AiConversationRecordDto?> UpdateAsync(long id, UpdateAiConversationRecordRequest request);

    Task<DeleteResultDto> DeleteAsync(long id);
}
