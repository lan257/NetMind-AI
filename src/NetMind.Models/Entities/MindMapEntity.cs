namespace NetMind.Models.Entities;

/// <summary>
/// Represents a mind map aggregate root.
/// </summary>
public sealed class MindMapEntity
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public long? RootNodeId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
