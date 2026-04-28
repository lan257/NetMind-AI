namespace NetMind.Models.Dtos;

public sealed class MindMapDto
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public long? RootNodeId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class CreateMindMapRequest
{
    public string Title { get; init; } = string.Empty;
}

public sealed class UpdateMindMapRequest
{
    public string Title { get; init; } = string.Empty;

    public long? RootNodeId { get; init; }
}
