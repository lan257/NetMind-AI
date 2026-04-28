using NetMind.Models.ViewModels;

namespace NetMind.Repository.Interfaces;

/// <summary>
/// Provides project status data for the minimum runnable application.
/// </summary>
public interface IProjectStatusRepository
{
    /// <summary>
    /// Gets the current project status.
    /// </summary>
    /// <returns>The current project status.</returns>
    Task<ProjectStatusViewModel> GetStatusAsync();
}
