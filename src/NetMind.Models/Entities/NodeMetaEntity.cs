namespace NetMind.Models.Entities;

/// <summary>
/// Represents a key-value metadata item attached to a node.
/// </summary>
public sealed class NodeMetaEntity
{
    public long NodeId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
