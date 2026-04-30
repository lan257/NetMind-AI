using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;

namespace NetMind.WebApi.Controllers;

[ApiController]
[Route("api/ai-conversation-records")]
public sealed class AiConversationRecordsController : ControllerBase
{
    private readonly IAiConversationRecordService _service;

    public AiConversationRecordsController(IAiConversationRecordService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ApiResult<IReadOnlyList<AiConversationRecordDto>>> ListAsync([FromQuery] string? conversationId)
    {
        return ApiResult<IReadOnlyList<AiConversationRecordDto>>.Ok(await _service.ListAsync(conversationId), "查询成功");
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResult<AiConversationRecordDto>>> GetAsync(long id)
    {
        var record = await _service.GetAsync(id);
        return record is null
            ? NotFound(ApiResult<AiConversationRecordDto>.Fail("AI 对话记录不存在。"))
            : ApiResult<AiConversationRecordDto>.Ok(record, "查询成功");
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<AiConversationRecordDto>>> CreateAsync(CreateAiConversationRecordRequest request)
    {
        try
        {
            var created = await _service.CreateAsync(request);
            return StatusCode(StatusCodes.Status201Created, ApiResult<AiConversationRecordDto>.Ok(created, "创建成功"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResult<AiConversationRecordDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResult<AiConversationRecordDto>>> UpdateAsync(long id, UpdateAiConversationRecordRequest request)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, request);
            return updated is null
                ? NotFound(ApiResult<AiConversationRecordDto>.Fail("AI 对话记录不存在。"))
                : ApiResult<AiConversationRecordDto>.Ok(updated, "保存成功");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResult<AiConversationRecordDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResult<DeleteResultDto>>> DeleteAsync(long id)
    {
        var result = await _service.DeleteAsync(id);
        return result.Deleted
            ? ApiResult<DeleteResultDto>.Ok(result, "删除成功")
            : NotFound(ApiResult<DeleteResultDto>.Fail("AI 对话记录不存在。"));
    }
}
