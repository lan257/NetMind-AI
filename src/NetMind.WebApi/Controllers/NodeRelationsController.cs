using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;

namespace NetMind.WebApi.Controllers;

[ApiController]
[Route("api/node-relations")]
public sealed class NodeRelationsController : ControllerBase
{
    private readonly INodeRelationService _nodeRelationService;

    public NodeRelationsController(INodeRelationService nodeRelationService)
    {
        _nodeRelationService = nodeRelationService;
    }

    [HttpGet("by-map/{mapId:long}")]
    public async Task<ApiResult<IReadOnlyList<NodeRelationDto>>> ListByMapAsync(long mapId)
    {
        return ApiResult<IReadOnlyList<NodeRelationDto>>.Ok(await _nodeRelationService.ListByMapAsync(mapId));
    }

    [HttpGet("by-node/{nodeId:long}")]
    public async Task<ApiResult<IReadOnlyList<NodeRelationDto>>> ListByNodeAsync(long nodeId)
    {
        return ApiResult<IReadOnlyList<NodeRelationDto>>.Ok(await _nodeRelationService.ListByNodeAsync(nodeId));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<NodeRelationDto>>> GetAsync(long id)
    {
        var relation = await _nodeRelationService.GetAsync(id);
        return relation is null ? NotFound(ApiResult<NodeRelationDto>.Fail("节点关联不存在。")) : ApiResult<NodeRelationDto>.Ok(relation);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<NodeRelationDto>>> CreateAsync(CreateNodeRelationRequest request)
    {
        try
        {
            var created = await _nodeRelationService.CreateAsync(request);
            return StatusCode(StatusCodes.Status201Created, ApiResult<NodeRelationDto>.Ok(created));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return BadRequest(ApiResult<NodeRelationDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<NodeRelationDto>>> UpdateAsync(long id, UpdateNodeRelationRequest request)
    {
        try
        {
            var updated = await _nodeRelationService.UpdateAsync(id, request);
            return updated is null ? NotFound(ApiResult<NodeRelationDto>.Fail("节点关联不存在。")) : ApiResult<NodeRelationDto>.Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return BadRequest(ApiResult<NodeRelationDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<DeleteResultDto>>> DeleteAsync(long id)
    {
        var result = await _nodeRelationService.DeleteAsync(id);
        return result.Deleted ? ApiResult<DeleteResultDto>.Ok(result) : NotFound(ApiResult<DeleteResultDto>.Fail("节点关联不存在。"));
    }

    [HttpDelete("by-node/{nodeId:long}")]
    public async Task<ApiResult<DeleteResultDto>> DeleteByNodeAsync(long nodeId)
    {
        return ApiResult<DeleteResultDto>.Ok(await _nodeRelationService.DeleteByNodeAsync(nodeId));
    }
}
