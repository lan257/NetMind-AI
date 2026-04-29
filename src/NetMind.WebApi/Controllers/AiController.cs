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
}
