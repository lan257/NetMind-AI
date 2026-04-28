using NetMind.Models.ViewModels;

namespace NetMind.Services.Interfaces;

/// <summary>
/// Provides application status operations.
/// </summary>
public interface IProjectStatusService
{
    /// <summary>
    /// Gets the current application status.
    /// </summary>
    /// <returns>The current application status.</returns>
    Task<ProjectStatusViewModel> GetStatusAsync();
}
