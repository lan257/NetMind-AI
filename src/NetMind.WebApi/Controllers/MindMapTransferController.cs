using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;

namespace NetMind.WebApi.Controllers;

[ApiController]
[Route("api/mind-map-transfer")]
public sealed class MindMapTransferController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IMindMapTransferService _transferService;

    public MindMapTransferController(IMindMapTransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpGet("{mapId:long}/structure")]
    public async Task<ActionResult<ApiResult<MindMapStructureDto>>> ExportStructureAsync(long mapId)
    {
        var structure = await _transferService.ExportAsync(mapId);
        return structure is null
            ? NotFound(ApiResult<MindMapStructureDto>.Fail("导图不存在。"))
            : ApiResult<MindMapStructureDto>.Ok(structure);
    }

    [HttpGet("{mapId:long}/file")]
    public async Task<IActionResult> ExportFileAsync(long mapId)
    {
        var structure = await _transferService.ExportAsync(mapId);
        if (structure is null)
        {
            return NotFound(ApiResult<MindMapStructureDto>.Fail("导图不存在。"));
        }

        return JsonFile(structure.Transfer, CreateExportFileName(structure, mapId));
    }

    [HttpPost("structure")]
    public async Task<ActionResult<ApiResult<ImportedMindMapDto>>> ImportStructureAsync(ImportMindMapRequest request)
    {
        try
        {
            return StatusCode(StatusCodes.Status201Created, ApiResult<ImportedMindMapDto>.Ok(await _transferService.ImportAsync(request)));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ApiResult<ImportedMindMapDto>.Fail(ex.Message));
        }
    }

    [HttpPost("file")]
    public async Task<ActionResult<ApiResult<ImportedMindMapDto>>> ImportFileAsync(IFormFile file, [FromForm] string? titleOverride)
    {
        if (file.Length == 0)
        {
            return BadRequest(ApiResult<ImportedMindMapDto>.Fail("请上传导入文件。"));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var request = await ReadImportRequestAsync(stream, titleOverride);
            return StatusCode(StatusCodes.Status201Created, ApiResult<ImportedMindMapDto>.Ok(await _transferService.ImportAsync(request)));
        }
        catch (JsonException ex)
        {
            return BadRequest(ApiResult<ImportedMindMapDto>.Fail($"Invalid JSON: {ex.Message}"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ApiResult<ImportedMindMapDto>.Fail(ex.Message));
        }
    }

    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        return JsonFile(_transferService.CreateTemplate(), "netmind-import-template.json");
    }

    private static async Task<ImportMindMapRequest> ReadImportRequestAsync(Stream stream, string? titleOverride)
    {
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement.GetRawText();
        if (document.RootElement.TryGetProperty("mindMap", out _))
        {
            var request = JsonSerializer.Deserialize<ImportMindMapRequest>(root, JsonOptions)
                ?? throw new ArgumentException("导入请求不能为空。");
            return new ImportMindMapRequest
            {
                MindMap = request.MindMap,
                TitleOverride = string.IsNullOrWhiteSpace(titleOverride) ? request.TitleOverride : titleOverride
            };
        }

        var transfer = JsonSerializer.Deserialize<MindMapTransferDto>(root, JsonOptions)
            ?? throw new ArgumentException("导图传输结构不能为空。");
        return new ImportMindMapRequest
        {
            MindMap = transfer,
            TitleOverride = titleOverride
        };
    }

    private static FileContentResult JsonFile<T>(T value, string fileName)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return new FileContentResult(json, "application/json")
        {
            FileDownloadName = fileName
        };
    }

    private static string CreateExportFileName(MindMapStructureDto structure, long mapId)
    {
        var title = string.IsNullOrWhiteSpace(structure.Map.Title)
            ? structure.Transfer.Title
            : structure.Map.Title;
        var fileStem = SanitizeFileName(title);
        return $"{(string.IsNullOrWhiteSpace(fileStem) ? $"netmind-map-{mapId}" : fileStem)}.json";
    }

    private static string SanitizeFileName(string? value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeCharacters = (value ?? string.Empty)
            .Trim()
            .Select(character =>
                invalidCharacters.Contains(character) || "<>:\"/\\|?*".Contains(character)
                    ? '_'
                    : character)
            .ToArray();
        return new string(safeCharacters).Trim().TrimEnd('.');
    }
}
