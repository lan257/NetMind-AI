using NetMind.Models.ViewModels;
using NetMind.Repository.Interfaces;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

/// <summary>
/// Coordinates application status queries.
/// </summary>
public sealed class ProjectStatusService : IProjectStatusService
{
    private readonly IProjectStatusRepository _projectStatusRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectStatusService"/> class.
    /// </summary>
    /// <param name="projectStatusRepository">The project status repository.</param>
    public ProjectStatusService(IProjectStatusRepository projectStatusRepository)
    {
        _projectStatusRepository = projectStatusRepository;
    }

    /// <inheritdoc />
    public Task<ProjectStatusViewModel> GetStatusAsync()
    {
        return _projectStatusRepository.GetStatusAsync();
    }
}
