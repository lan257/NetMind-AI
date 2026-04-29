using NetMind.Models.Dtos;

namespace NetMind.Services.Interfaces;

public interface IMindMapTransferService
{
    Task<MindMapStructureDto?> ExportAsync(long mapId);

    Task<ImportedMindMapDto> ImportAsync(ImportMindMapRequest request);

    MindMapTransferDto CreateTemplate();
}
