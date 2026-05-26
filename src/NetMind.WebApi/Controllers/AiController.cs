using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;
using NetMind.WebApi.Security;

namespace NetMind.WebApi.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private const string FixedAgentDomain = "netmind";

    private readonly IAiCleanService _aiCleanService;
    private readonly IAiAgentService _aiAgentService;
    private readonly IAiConversationRecordService _conversationRecordService;
    private readonly ApiKeyEncryptionService _apiKeyEncryptionService;

    public AiController(
        IAiCleanService aiCleanService,
        IAiAgentService aiAgentService,
        IAiConversationRecordService conversationRecordService,
        ApiKeyEncryptionService apiKeyEncryptionService)
    {
        _aiCleanService = aiCleanService;
        _aiAgentService = aiAgentService;
        _conversationRecordService = conversationRecordService;
        _apiKeyEncryptionService = apiKeyEncryptionService;
    }

    [HttpGet("models")]
    public ApiResult<IReadOnlyList<AiModelOptionDto>> ListModels()
    {
        return ApiResult<IReadOnlyList<AiModelOptionDto>>.Ok(_aiCleanService.ListModels());
    }

    [HttpPost("clean")]
    public async Task<ActionResult<ApiResult<AiCleanResultDto>>> CleanAsync(AiCleanRequest request)
    {
        try
        {
            DecryptApiKey(request);
            return ApiResult<AiCleanResultDto>.Ok(await _aiCleanService.CleanAsync(request));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return BadRequest(ApiResult<AiCleanResultDto>.Fail(ex.Message));
        }
    }

    [HttpPost("requirements/structure")]
    public async Task<ActionResult<ApiResult<AiRequirementStructureResultDto>>> StructureRequirementAsync(AiRequirementStructureRequest request)
    {
        try
        {
            DecryptApiKey(request);
            return ApiResult<AiRequirementStructureResultDto>.Ok(await _aiCleanService.StructureRequirementAsync(request));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return BadRequest(ApiResult<AiRequirementStructureResultDto>.Fail(ex.Message));
        }
    }

    [HttpPost("node-chat")]
    public async Task<ActionResult<ApiResult<AiNodeChatResult>>> ChatWithNodeAsync(AiNodeChatRequest request)
    {
        try
        {
            DecryptApiKey(request);
            var result = await _aiCleanService.ChatWithNodeAsync(request);
            if (!string.IsNullOrWhiteSpace(request.ConversationId))
            {
                await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
                {
                    ConversationId = request.ConversationId,
                    Role = "user",
                    Content = request.Message,
                    ModelId = request.ModelId
                });
                await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
                {
                    ConversationId = request.ConversationId,
                    Role = "assistant",
                    Content = result.Reply,
                    ModelId = result.SelectedModel.Id,
                    Prompt = result.Prompt,
                    WasContextCompressed = result.WasContextCompressed
                });
            }

            return ApiResult<AiNodeChatResult>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return BadRequest(ApiResult<AiNodeChatResult>.Fail(ex.Message));
        }
    }

    [HttpPost("map-chat")]
    public async Task<ActionResult<ApiResult<AiMapChatResult>>> ChatWithMapAsync(AiMapChatRequest request)
    {
        try
        {
            DecryptApiKey(request);
            var result = await _aiCleanService.ChatWithMapAsync(request);
            if (!string.IsNullOrWhiteSpace(request.ConversationId))
            {
                await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
                {
                    ConversationId = request.ConversationId,
                    Role = "user",
                    Content = request.Message,
                    ModelId = request.ModelId
                });
                await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
                {
                    ConversationId = request.ConversationId,
                    Role = "assistant",
                    Content = result.Reply,
                    ModelId = result.SelectedModel.Id,
                    Prompt = result.Prompt,
                    WasContextCompressed = result.WasContextCompressed
                });
            }

            return ApiResult<AiMapChatResult>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return BadRequest(ApiResult<AiMapChatResult>.Fail(ex.Message));
        }
    }

    [HttpPost("node-agent-chat")]
    public async Task<ActionResult<ApiResult<AiAgentChatResult>>> ChatWithNodeAgentAsync(AiNodeAgentChatRequest request)
    {
        try
        {
            DecryptApiKey(request);
            ApplyFixedAgentDomain(request);
            var result = await _aiAgentService.ChatWithNodeAgentAsync(request);
            await SaveAgentConversationAsync(request, result);

            return ApiResult<AiAgentChatResult>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return BadRequest(ApiResult<AiAgentChatResult>.Fail(ex.Message));
        }
    }

    [HttpPost("map-agent-chat")]
    public async Task<ActionResult<ApiResult<AiAgentChatResult>>> ChatWithMapAgentAsync(AiMapAgentChatRequest request)
    {
        try
        {
            DecryptApiKey(request);
            ApplyFixedAgentDomain(request);
            var result = await _aiAgentService.ChatWithMapAgentAsync(request);
            await SaveAgentConversationAsync(request, result);

            return ApiResult<AiAgentChatResult>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return BadRequest(ApiResult<AiAgentChatResult>.Fail(ex.Message));
        }
    }

    [HttpPost("global-agent-chat")]
    public async Task<ActionResult<ApiResult<AiAgentChatResult>>> ChatWithGlobalAgentAsync(AiGlobalAgentChatRequest request)
    {
        try
        {
            DecryptApiKey(request);
            ApplyFixedAgentDomain(request);
            var result = await _aiAgentService.ChatWithGlobalAgentAsync(request);
            await SaveAgentConversationAsync(request, result);

            return ApiResult<AiAgentChatResult>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return BadRequest(ApiResult<AiAgentChatResult>.Fail(ex.Message));
        }
    }

    [HttpPost("app-help-agent-chat")]
    public async Task<ActionResult<ApiResult<AiAgentChatResult>>> ChatWithAppHelpAgentAsync(AiAppHelpAgentChatRequest request)
    {
        try
        {
            DecryptApiKey(request);
            ApplyFixedAgentDomain(request);
            var result = await _aiAgentService.ChatWithAppHelpAgentAsync(request);
            await SaveAgentConversationAsync(request, result);

            return ApiResult<AiAgentChatResult>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return BadRequest(ApiResult<AiAgentChatResult>.Fail(ex.Message));
        }
    }

    [HttpPost("app-help-chat")]
    public async Task<ActionResult<ApiResult<AiAppHelpResult>>> ChatForAppHelpAsync(AiAppHelpRequest request)
    {
        try
        {
            DecryptApiKey(request);
            var result = await _aiCleanService.ChatForAppHelpAsync(request);
            if (!string.IsNullOrWhiteSpace(request.ConversationId))
            {
                await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
                {
                    ConversationId = request.ConversationId,
                    Role = "user",
                    Content = request.Message,
                    ModelId = request.ModelId
                });
                await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
                {
                    ConversationId = request.ConversationId,
                    Role = "assistant",
                    Content = result.Reply,
                    ModelId = result.SelectedModel.Id,
                    Prompt = result.Prompt,
                    WasContextCompressed = result.WasContextCompressed
                });
            }

            return ApiResult<AiAppHelpResult>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return BadRequest(ApiResult<AiAppHelpResult>.Fail(ex.Message));
        }
    }

    [HttpPost("context-chat")]
    public async Task<ActionResult<ApiResult<AiContextChatResultDto>>> ChatWithContextAsync(AiContextChatRequest request)
    {
        try
        {
            DecryptApiKey(request);
            var result = await _aiCleanService.ChatWithContextAsync(request);
            if (!string.IsNullOrWhiteSpace(request.ConversationId))
            {
                await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
                {
                    ConversationId = request.ConversationId,
                    Role = "user",
                    Content = request.Message,
                    ModelId = request.ModelId
                });
                await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
                {
                    ConversationId = request.ConversationId,
                    Role = "assistant",
                    Content = result.Reply,
                    ModelId = result.SelectedModel.Id,
                    Prompt = result.Prompt,
                    ContextSummary = result.ContextSummary,
                    WasContextCompressed = result.WasContextCompressed
                });
            }

            return ApiResult<AiContextChatResultDto>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return BadRequest(ApiResult<AiContextChatResultDto>.Fail(ex.Message));
        }
    }

    private async Task SaveAgentConversationAsync(AiAgentChatRequest request, AiAgentChatResult result)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return;
        }

        await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
        {
            ConversationId = request.ConversationId,
            Role = "user",
            Content = string.IsNullOrWhiteSpace(request.Message) ? "用户处理了 Agent Tool 权限。" : request.Message,
            ModelId = request.ModelId
        });
        await _conversationRecordService.CreateAsync(new CreateAiConversationRecordRequest
        {
            ConversationId = request.ConversationId,
            Role = "assistant",
            Content = result.Reply,
            ModelId = result.SelectedModel.Id,
            Prompt = result.Prompt,
            WasContextCompressed = result.WasContextCompressed
        });
    }

    private static void ApplyFixedAgentDomain(AiAgentChatRequest request)
    {
        request.Domain = FixedAgentDomain;
    }

    private void DecryptApiKey(AiCleanRequest request)
    {
        request.ApiKey = _apiKeyEncryptionService.DecryptIfEncrypted(request.ApiKey);
    }

    private void DecryptApiKey(AiRequirementStructureRequest request)
    {
        request.ApiKey = _apiKeyEncryptionService.DecryptIfEncrypted(request.ApiKey);
    }

    private void DecryptApiKey(AiContextChatRequest request)
    {
        request.ApiKey = _apiKeyEncryptionService.DecryptIfEncrypted(request.ApiKey);
    }

    private void DecryptApiKey(AiNodeChatRequest request)
    {
        request.ApiKey = _apiKeyEncryptionService.DecryptIfEncrypted(request.ApiKey);
    }

    private void DecryptApiKey(AiMapChatRequest request)
    {
        request.ApiKey = _apiKeyEncryptionService.DecryptIfEncrypted(request.ApiKey);
    }

    private void DecryptApiKey(AiAppHelpRequest request)
    {
        request.ApiKey = _apiKeyEncryptionService.DecryptIfEncrypted(request.ApiKey);
    }

    private void DecryptApiKey(AiAgentChatRequest request)
    {
        request.ApiKey = _apiKeyEncryptionService.DecryptIfEncrypted(request.ApiKey);
    }
}
