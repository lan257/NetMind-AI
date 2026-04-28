namespace NetMind.Models.ViewModels;

/// <summary>
/// Describes the minimum runnable status of the NetMind application.
/// </summary>
public sealed class ProjectStatusViewModel
{
    /// <summary>
    /// Gets the project name.
    /// </summary>
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current milestone.
    /// </summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>
    /// Gets the API runtime target.
    /// </summary>
    public string Runtime { get; init; } = string.Empty;

    /// <summary>
    /// Gets the frontend shell status.
    /// </summary>
    public string Frontend { get; init; } = string.Empty;
}
