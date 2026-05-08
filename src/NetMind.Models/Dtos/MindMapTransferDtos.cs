namespace NetMind.Models.Dtos;

public sealed class MindMapTransferDto
{
    public string SchemaVersion { get; init; } = "netmind.mindmap.v1";

    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<MindMapTransferNodeDto> Nodes { get; init; } = Array.Empty<MindMapTransferNodeDto>();

    public IReadOnlyList<MindMapTransferRelationDto> Relations { get; init; } = Array.Empty<MindMapTransferRelationDto>();
}

public sealed class MindMapTransferNodeDto
{
    public string ClientId { get; init; } = string.Empty;

    public string? ParentClientId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Content { get; init; }

    public int OrderNo { get; init; }

    public double? PositionX { get; init; }

    public double? PositionY { get; init; }
}

public sealed class MindMapTransferRelationDto
{
    public string SourceClientId { get; init; } = string.Empty;

    public string TargetClientId { get; init; } = string.Empty;

    public string RelationType { get; init; } = "relates_to";

    public double Weight { get; init; } = 1;
}

public sealed class ImportMindMapRequest
{
    public MindMapTransferDto MindMap { get; init; } = new();

    public string? TitleOverride { get; init; }
}

public sealed class MindMapStructureDto
{
    public MindMapDto Map { get; init; } = new();

    public IReadOnlyList<NodeDto> Nodes { get; init; } = Array.Empty<NodeDto>();

    public IReadOnlyList<NodeRelationDto> Relations { get; init; } = Array.Empty<NodeRelationDto>();

    public MindMapTransferDto Transfer { get; init; } = new();
}

public sealed class ImportedMindMapDto
{
    public MindMapStructureDto Structure { get; init; } = new();

    public IReadOnlyDictionary<string, long> NodeIdMap { get; init; } = new Dictionary<string, long>();
}
