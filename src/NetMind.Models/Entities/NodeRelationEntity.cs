namespace NetMind.Models.Entities;

/// <summary>
/// Represents a directed relation between two nodes.
/// </summary>
public sealed class NodeRelationEntity
{
    public long Id { get; set; }

    public long SourceId { get; set; }

    public long TargetId { get; set; }

    public string RelationType { get; set; } = string.Empty;

    public double Weight { get; set; }

    public long MapId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
