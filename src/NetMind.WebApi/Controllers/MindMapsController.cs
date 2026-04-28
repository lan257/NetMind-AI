using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;

namespace NetMind.WebApi.Controllers;

[ApiController]
[Route("api/mind-maps")]
public sealed class MindMapsController : ControllerBase
{
    private readonly IMindMapService _mindMapService;

    public MindMapsController(IMindMapService mindMapService)
    {
        _mindMapService = mindMapService;
    }

    [HttpGet]
    public async Task<ApiResult<IReadOnlyList<MindMapDto>>> ListAsync()
    {
        return ApiResult<IReadOnlyList<MindMapDto>>.Ok(await _mindMapService.ListAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<MindMapDto>>> GetAsync(long id)
    {
        var map = await _mindMapService.GetAsync(id);
        return map is null ? NotFound(ApiResult<MindMapDto>.Fail("Mind map not found.")) : ApiResult<MindMapDto>.Ok(map);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<MindMapDto>>> CreateAsync(CreateMindMapRequest request)
    {
        try
        {
            var created = await _mindMapService.CreateAsync(request);
            return CreatedAtAction(nameof(GetAsync), new { id = created.Id }, ApiResult<MindMapDto>.Ok(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResult<MindMapDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<MindMapDto>>> UpdateAsync(long id, UpdateMindMapRequest request)
    {
        try
        {
            var updated = await _mindMapService.UpdateAsync(id, request);
            return updated is null ? NotFound(ApiResult<MindMapDto>.Fail("Mind map or root node not found.")) : ApiResult<MindMapDto>.Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResult<MindMapDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<DeleteResultDto>>> DeleteAsync(long id)
    {
        var result = await _mindMapService.DeleteAsync(id, cascade: false);
        return result.Deleted ? ApiResult<DeleteResultDto>.Ok(result) : NotFound(ApiResult<DeleteResultDto>.Fail("Mind map not found."));
    }

    [HttpDelete("{id:long}/cascade")]
    public async Task<ActionResult<ApiResult<DeleteResultDto>>> DeleteCascadeAsync(long id)
    {
        var result = await _mindMapService.DeleteAsync(id, cascade: true);
        return result.Deleted ? ApiResult<DeleteResultDto>.Ok(result) : NotFound(ApiResult<DeleteResultDto>.Fail("Mind map not found."));
    }
}
