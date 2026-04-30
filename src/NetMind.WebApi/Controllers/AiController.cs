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

    public AiController(IAiCleanService aiCleanService)
    {
        _aiCleanService = aiCleanService;
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

    [HttpPost("context-chat")]
    public async Task<ActionResult<ApiResult<AiContextChatResultDto>>> ChatWithContextAsync(AiContextChatRequest request)
    {
        try
        {
            return ApiResult<AiContextChatResultDto>.Ok(await _aiCleanService.ChatWithContextAsync(request));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return BadRequest(ApiResult<AiContextChatResultDto>.Fail(ex.Message));
        }
    }
}
