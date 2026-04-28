namespace NetMind.Models.Dtos;

public sealed class NodeDto
{
    public long Id { get; init; }

    public long MapId { get; init; }

    public long? ParentId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Content { get; init; }

    public int OrderNo { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class CreateNodeRequest
{
    public long MapId { get; init; }

    public long? ParentId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Content { get; init; }

    public int OrderNo { get; init; }
}

public sealed class UpdateNodeRequest
{
    public long? ParentId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Content { get; init; }

    public int OrderNo { get; init; }
}
