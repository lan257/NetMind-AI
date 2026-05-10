using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;

namespace NetMind.WebApi.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly IAiCleanService _aiCleanService;
    private readonly IAiConversationRecordService _conversationRecordService;

    public AiController(IAiCleanService aiCleanService, IAiConversationRecordService conversationRecordService)
    {
        _aiCleanService = aiCleanService;
        _conversationRecordService = conversationRecordService;
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

    [HttpPost("app-help-chat")]
    public async Task<ActionResult<ApiResult<AiAppHelpResult>>> ChatForAppHelpAsync(AiAppHelpRequest request)
    {
        try
        {
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
}