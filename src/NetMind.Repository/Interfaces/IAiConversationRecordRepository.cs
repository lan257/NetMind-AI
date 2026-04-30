using NetMind.Models.Entities;

namespace NetMind.Repository.Interfaces;

public interface IAiConversationRecordRepository
{
    Task<IReadOnlyList<AiConversationRecordEntity>> ListAsync(string? conversationId);

    Task<AiConversationRecordEntity?> GetAsync(long id);

    Task<AiConversationRecordEntity> CreateAsync(
        string conversationId,
        string role,
        string content,
        string? modelId,
        string? prompt,
        string? contextSummary,
        bool wasContextCompressed);

    Task<AiConversationRecordEntity?> UpdateAsync(
        long id,
        string role,
        string content,
        string? modelId,
        string? prompt,
        string? contextSummary,
        bool wasContextCompressed);

    Task<int> DeleteAsync(long id);
}
