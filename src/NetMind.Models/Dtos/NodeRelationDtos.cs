namespace NetMind.Models.Dtos;

public sealed class NodeRelationDto
{
    public long Id { get; init; }

    public long SourceId { get; init; }

    public string? SourceTitle { get; init; }

    public long? SourceMapId { get; init; }

    public long TargetId { get; init; }

    public string? TargetTitle { get; init; }

    public long? TargetMapId { get; init; }

    public string RelationType { get; init; } = string.Empty;

    public double Weight { get; init; }

    public long MapId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateNodeRelationRequest
{
    public long SourceId { get; init; }

    public long TargetId { get; init; }

    public string RelationType { get; init; } = string.Empty;

    public double Weight { get; init; } = 1;

    public long MapId { get; init; }
}

public sealed class UpdateNodeRelationRequest
{
    public string RelationType { get; init; } = string.Empty;

    public double Weight { get; init; } = 1;
}
