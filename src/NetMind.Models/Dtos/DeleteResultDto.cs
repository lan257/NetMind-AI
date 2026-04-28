namespace NetMind.Models.Dtos;

public sealed class DeleteResultDto
{
    public bool Deleted { get; init; }

    public int AffectedCount { get; init; }
}
