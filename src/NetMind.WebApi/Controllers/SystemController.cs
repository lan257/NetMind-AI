using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.ViewModels;
using NetMind.Services.Interfaces;

namespace NetMind.WebApi.Controllers;

/// <summary>
/// Provides system-level endpoints.
/// </summary>
[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    private readonly IProjectStatusService _projectStatusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemController"/> class.
    /// </summary>
    /// <param name="projectStatusService">The project status service.</param>
    public SystemController(IProjectStatusService projectStatusService)
    {
        _projectStatusService = projectStatusService;
    }

    /// <summary>
    /// Gets the API health status.
    /// </summary>
    /// <returns>The current application status.</returns>
    [HttpGet("health")]
    public async Task<ApiResult<ProjectStatusViewModel>> GetHealthAsync()
    {
        var status = await _projectStatusService.GetStatusAsync();
        return ApiResult<ProjectStatusViewModel>.Ok(status);
    }
}
