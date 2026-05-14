using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface IAiAgentService
{
    Task<AiAgentChatResult> ChatWithNodeAgentAsync(AiNodeAgentChatRequest request);

    Task<AiAgentChatResult> ChatWithMapAgentAsync(AiMapAgentChatRequest request);

    Task<AiAgentChatResult> ChatWithGlobalAgentAsync(AiGlobalAgentChatRequest request);

    Task<AiAgentChatResult> ChatWithAppHelpAgentAsync(AiAppHelpAgentChatRequest request);
}
