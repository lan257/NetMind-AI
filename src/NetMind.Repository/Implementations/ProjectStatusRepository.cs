using NetMind.Models.ViewModels;
using NetMind.Repository.Interfaces;

namespace NetMind.Repository.Implementations;

/// <summary>
/// In-memory repository used before database persistence is introduced.
/// </summary>
public sealed class ProjectStatusRepository : IProjectStatusRepository
{
    /// <inheritdoc />
    public Task<ProjectStatusViewModel> GetStatusAsync()
    {
        var status = new ProjectStatusViewModel
        {
            ProjectName = "NetMind",
            Phase = "P1.1",
            Runtime = ".NET 8",
            Frontend = "Vue3/HTML5 shell"
        };

        return Task.FromResult(status);
    }
}
