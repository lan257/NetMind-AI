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
    public ActionResult<ApiResult<AiCleanResultDto>> Clean(AiCleanRequest request)
    {
        try
        {
            return ApiResult<AiCleanResultDto>.Ok(_aiCleanService.Clean(request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResult<AiCleanResultDto>.Fail(ex.Message));
        }
    }
}
