using NetMind.Models.ViewModels;
using NetMind.Repository.Interfaces;

namespace NetMind.Repository.Implementations;

public sealed class ProjectStatusRepository : IProjectStatusRepository
{
    /// <inheritdoc />
    public Task<ProjectStatusViewModel> GetStatusAsync()
    {
        var status = new ProjectStatusViewModel
        {
            ProjectName = "NetMind",
            Phase = "P1.4",
            Runtime = ".NET 8",
            Frontend = "Vue3/HTML5 shell"
        };

        return Task.FromResult(status);
    }
}
