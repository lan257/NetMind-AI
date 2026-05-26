using Microsoft.AspNetCore.Mvc;
using NetMind.Common.Responses;
using NetMind.Models.ViewModels;
using NetMind.Services.Interfaces;
using NetMind.WebApi.Security;

namespace NetMind.WebApi.Controllers;

/// <summary>
/// Provides system-level endpoints.
/// </summary>
[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    private readonly IProjectStatusService _projectStatusService;
    private readonly ApiKeyEncryptionService _apiKeyEncryptionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemController"/> class.
    /// </summary>
    /// <param name="projectStatusService">The project status service.</param>
    public SystemController(
        IProjectStatusService projectStatusService,
        ApiKeyEncryptionService apiKeyEncryptionService)
    {
        _projectStatusService = projectStatusService;
        _apiKeyEncryptionService = apiKeyEncryptionService;
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

    [HttpGet("crypto/api-key-public-key")]
    public ApiResult<ApiKeyPublicKeyDto> GetApiKeyPublicKey()
    {
        return ApiResult<ApiKeyPublicKeyDto>.Ok(_apiKeyEncryptionService.GetPublicKey());
    }
}
