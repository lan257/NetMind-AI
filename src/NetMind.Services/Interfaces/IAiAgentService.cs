using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface IAiAgentService
{
    Task<AiAgentChatResult> ChatWithNodeAgentAsync(AiNodeAgentChatRequest request);
}
