namespace NetMind.Models.Entities;

/// <summary>
/// Represents a node in a mind map tree.
/// </summary>
public sealed class NodeEntity
{
    public long Id { get; set; }

    public long MapId { get; set; }

    public string? MapTitle { get; set; }

    public long? ParentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }

    public int OrderNo { get; set; }

    public double? PositionX { get; set; }

    public double? PositionY { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
