using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;

namespace NetMind.WebApi.Controllers;

[ApiController]
[Route("api/nodes")]
public sealed class NodesController : ControllerBase
{
    private readonly INodeService _nodeService;
    private readonly IExploreNodeService _exploreService;

    public NodesController(INodeService nodeService, IExploreNodeService exploreService)
    {
        _exploreService = exploreService;
        _nodeService = nodeService;
    }

    [HttpGet("by-map/{mapId:long}")]
    public async Task<ApiResult<IReadOnlyList<NodeDto>>> ListByMapAsync(long mapId)
    {
        return ApiResult<IReadOnlyList<NodeDto>>.Ok(await _nodeService.ListByMapAsync(mapId));
    }

    [HttpGet("search")]
    public async Task<ApiResult<IReadOnlyList<NodeDto>>> SearchAsync([FromQuery] long? mapId, [FromQuery] string keyword, [FromQuery] int limit = 10)
    {
        return ApiResult<IReadOnlyList<NodeDto>>.Ok(await _nodeService.SearchAsync(mapId, keyword, limit));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<NodeDto>>> GetAsync(long id)
    {
        var node = await _nodeService.GetAsync(id);
        return node is null ? NotFound(ApiResult<NodeDto>.Fail("节点不存在。")) : ApiResult<NodeDto>.Ok(node);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<NodeDto>>> CreateAsync(CreateNodeRequest request)
    {
        try
        {
            var created = await _nodeService.CreateAsync(request);
            return StatusCode(StatusCodes.Status201Created, ApiResult<NodeDto>.Ok(created));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ApiResult<NodeDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<NodeDto>>> UpdateAsync(long id, UpdateNodeRequest request)
    {
        try
        {
            var updated = await _nodeService.UpdateAsync(id, request);
            return updated is null ? NotFound(ApiResult<NodeDto>.Fail("节点或父节点不存在。")) : ApiResult<NodeDto>.Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ApiResult<NodeDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<DeleteResultDto>>> DeleteSelfAsync(long id)
    {
        var result = await _nodeService.DeleteSelfAsync(id);
        return result.Deleted ? ApiResult<DeleteResultDto>.Ok(result) : NotFound(ApiResult<DeleteResultDto>.Fail("节点不存在。"));
    }

    [HttpDelete("{id:long}/subtree")]
    public async Task<ActionResult<ApiResult<DeleteResultDto>>> DeleteSubtreeAsync(long id)
    {
        var result = await _nodeService.DeleteSubtreeAsync(id);
        return result.Deleted ? ApiResult<DeleteResultDto>.Ok(result) : NotFound(ApiResult<DeleteResultDto>.Fail("节点不存在。"));
    }
[HttpGet("{id:long}/explore")]
    public async Task<ActionResult<ApiResult<ExploreNodeResultDto>>> ExploreAsync(long id, [FromQuery] int depth = 2)
    {
        if (depth is < 1 or > 3)
        {
            return BadRequest(ApiResult<ExploreNodeResultDto>.Fail("探索深度必须在 1~3 之间。"));
        }

        ExploreNodeResultDto? result;
        try
        {
            result = await _exploreService.ExploreAsync(id, depth);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ApiResult<ExploreNodeResultDto>.Fail(ex.Message));
        }

        return result is null
            ? NotFound(ApiResult<ExploreNodeResultDto>.Fail("节点不存在。"))
            : ApiResult<ExploreNodeResultDto>.Ok(result);
    }
}
