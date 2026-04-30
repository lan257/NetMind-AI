using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed class AiConversationRecordService : IAiConversationRecordService
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "user",
        "assistant",
        "system"
    };

    private readonly IAiConversationRecordRepository _repository;

    public AiConversationRecordService(IAiConversationRecordRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AiConversationRecordDto>> ListAsync(string? conversationId)
    {
        return (await _repository.ListAsync(conversationId)).Select(ToDto).ToList();
    }

    public async Task<AiConversationRecordDto?> GetAsync(long id)
    {
        var entity = await _repository.GetAsync(id);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<AiConversationRecordDto> CreateAsync(CreateAiConversationRecordRequest request)
    {
        var conversationId = RequireText(request.ConversationId, nameof(request.ConversationId));
        var role = RequireRole(request.Role);
        var content = RequireText(request.Content, nameof(request.Content));

        return ToDto(await _repository.CreateAsync(
            conversationId,
            role,
            content,
            TrimToNull(request.ModelId),
            TrimToNull(request.Prompt),
            TrimToNull(request.ContextSummary),
            request.WasContextCompressed));
    }

    public async Task<AiConversationRecordDto?> UpdateAsync(long id, UpdateAiConversationRecordRequest request)
    {
        var role = RequireRole(request.Role);
        var content = RequireText(request.Content, nameof(request.Content));

        var entity = await _repository.UpdateAsync(
            id,
            role,
            content,
            TrimToNull(request.ModelId),
            TrimToNull(request.Prompt),
            TrimToNull(request.ContextSummary),
            request.WasContextCompressed);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<DeleteResultDto> DeleteAsync(long id)
    {
        var affected = await _repository.DeleteAsync(id);
        return new DeleteResultDto { Deleted = affected > 0, AffectedCount = affected };
    }

    private static string RequireRole(string value)
    {
        var role = RequireText(value, nameof(value)).ToLowerInvariant();
        if (!AllowedRoles.Contains(role))
        {
            throw new ArgumentException("对话角色只能是 user、assistant 或 system。", nameof(value));
        }

        return role;
    }

    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} 不能为空。", name);
        }

        return value.Trim();
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AiConversationRecordDto ToDto(AiConversationRecordEntity entity)
    {
        return new AiConversationRecordDto
        {
            Id = entity.Id,
            ConversationId = entity.ConversationId,
            Role = entity.Role,
            Content = entity.Content,
            ModelId = entity.ModelId,
            Prompt = entity.Prompt,
            ContextSummary = entity.ContextSummary,
            WasContextCompressed = entity.WasContextCompressed,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
