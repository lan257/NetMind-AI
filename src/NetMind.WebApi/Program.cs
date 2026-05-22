using Microsoft.Extensions.FileProviders;
using NetMind.Common.Responses;
using NetMind.Repository.Implementations;
using NetMind.Repository.Interfaces;
using NetMind.Services.Configurations;
using NetMind.Services.Implementations;
using NetMind.Services.Interfaces;
using NetMind.Common.Logging;
using NetMind.WebApi.Infrastructure;
using NetMind.WebApi.Middleware;
using NetMind.WebApi.Swagger;

var builder = WebApplication.CreateBuilder(args);
var appBaseUrl = builder.Configuration["App:BaseUrl"]
    ?? throw new InvalidOperationException("必须配置 App:BaseUrl。");

if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) &&
    string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls(appBaseUrl);
}

builder.Services.AddControllers();
builder.Services.AddSingleton<IAppLogger, AppLogger>();
builder.Services.AddSingleton<IProjectStatusRepository, ProjectStatusRepository>();
builder.Services.AddSingleton(_ => new PostgresConnectionFactory(
    Environment.GetEnvironmentVariable("PGSTR") ?? string.Empty));
builder.Services.AddScoped<IMindMapRepository, MindMapRepository>();
builder.Services.AddScoped<INodeRepository, NodeRepository>();
builder.Services.AddScoped<INodeRelationRepository, NodeRelationRepository>();
builder.Services.AddScoped<IAiConversationRecordRepository, AiConversationRecordRepository>();
builder.Services.AddScoped<IProjectStatusService, ProjectStatusService>();
builder.Services.AddScoped<IMindMapService, MindMapService>();
builder.Services.AddScoped<INodeService, NodeService>();
builder.Services.AddScoped<INodeRelationService, NodeRelationService>();
builder.Services.AddScoped<IMindMapTransferService, MindMapTransferService>();
builder.Services.AddScoped<IAiConversationRecordService, AiConversationRecordService>();
builder.Services.AddScoped<IAiAgentService, AiAgentService>();
builder.Services.AddHttpClient<IAiCleanService, AiCleanService>();
builder.Services.AddSingleton(LoadAiCleanOptions(builder.Configuration, builder.Environment.ContentRootPath));
builder.Services.AddSingleton(LoadAiAgentOptions(builder.Configuration, builder.Environment.ContentRootPath, appBaseUrl));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    StartFrontendDevServer(app, appBaseUrl);
}

app.UseMiddleware<ApiCallLoggingMiddleware>();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        var message = ex.GetType().Namespace?.StartsWith("Npgsql", StringComparison.Ordinal) == true
            ? $"数据库请求失败：{ex.Message}"
            : ex.Message;
        await context.Response.WriteAsJsonAsync(ApiResult<object>.Fail(message));
    }
});

app.UseRouting();

var frontendDistRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "NetMind.Frontend", "dist"));
if (!app.Environment.IsDevelopment() && Directory.Exists(frontendDistRoot))
{
    var frontendFiles = new PhysicalFileProvider(frontendDistRoot);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendFiles
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFiles
    });
}

app.MapControllers();
app.MapGet("/swagger/v1/swagger.json", () => Results.Json(SwaggerDocumentFactory.Create()));
app.MapGet("/swagger", () => Results.Content(SwaggerDocumentFactory.CreateHtml(), "text/html; charset=utf-8"));
app.MapGet("/swagger/index.html", () => Results.Content(SwaggerDocumentFactory.CreateHtml(), "text/html; charset=utf-8"));
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("http://localhost:5173"));
}

app.Run();

static void StartFrontendDevServer(WebApplication app, string appBaseUrl)
{
    const int frontendPort = 5173;
    if (IsFrontendDevServerRunning(frontendPort))
    {
        return;
    }

    var frontendRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "NetMind.Frontend"));
    if (!Directory.Exists(frontendRoot))
    {
        app.Logger.LogWarning("Frontend directory was not found: {FrontendRoot}", frontendRoot);
        return;
    }

    var startInfo = OperatingSystem.IsWindows()
        ? new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c npm run dev")
        : new System.Diagnostics.ProcessStartInfo("npm", "run dev");

    startInfo.WorkingDirectory = frontendRoot;
    startInfo.UseShellExecute = false;
    startInfo.RedirectStandardOutput = true;
    startInfo.RedirectStandardError = true;
    startInfo.CreateNoWindow = true;
    startInfo.Environment["VITE_API_PROXY_TARGET"] = ResolveFrontendProxyTarget(app, appBaseUrl);

    try
    {
        var process = System.Diagnostics.Process.Start(startInfo);
        if (process is null)
        {
            app.Logger.LogWarning("Failed to start the frontend dev server.");
            return;
        }

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                app.Logger.LogInformation("[frontend] {Message}", eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                app.Logger.LogWarning("[frontend] {Message}", eventArgs.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogDebug(ex, "Failed to stop the frontend dev server process.");
            }
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to start the frontend dev server.");
    }
}

static string ResolveFrontendProxyTarget(WebApplication app, string fallbackUrl)
{
    var configuredUrls = app.Configuration["urls"]
        ?? app.Configuration["ASPNETCORE_URLS"]
        ?? string.Empty;

    var candidates = app.Urls
        .Concat(configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Append(fallbackUrl);

    return candidates.FirstOrDefault(url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        ?? candidates.First();
}

static bool IsFrontendDevServerRunning(int port)
{
    return IsTcpPortOpen("127.0.0.1", port) || IsTcpPortOpen("::1", port);
}

static bool IsTcpPortOpen(string host, int port)
{
    try
    {
        using var client = new System.Net.Sockets.TcpClient();
        var connectTask = client.ConnectAsync(host, port);
        return connectTask.Wait(TimeSpan.FromMilliseconds(300)) && client.Connected;
    }
    catch
    {
        return false;
    }
}

static AiCleanOptions LoadAiCleanOptions(IConfiguration configuration, string contentRootPath)
{
    var promptSection = configuration.GetSection("AiClean:Prompt");
    var models = configuration.GetSection("AiClean:Models")
        .GetChildren()
        .Select(section => new AiModelOptions
        {
            Id = section["Id"] ?? string.Empty,
            Name = section["Name"] ?? string.Empty,
            Provider = section["Provider"] ?? string.Empty,
            Endpoint = section["Endpoint"] ?? string.Empty,
            Model = section["Model"] ?? string.Empty,
            Enabled = ReadBool(section["Enabled"]),
            IsDefault = ReadBool(section["IsDefault"]),
            ApiKey = section["ApiKey"],
            ApiKeyEnvironmentVariable = section["ApiKeyEnvironmentVariable"],
            TimeoutSeconds = ReadInt(section["TimeoutSeconds"], 60),
            Notes = section["Notes"] ?? string.Empty
        })
        .ToList();

    return new AiCleanOptions
    {
        Models = models,
        Prompt = new AiPromptOptions
        {
            ContextCompressionThreshold = ReadInt(promptSection["ContextCompressionThreshold"], 4000),
            SystemPromptLines = ReadPromptLines(promptSection, "System", "SystemPromptLines", contentRootPath),
            UserPromptTemplateLines = ReadPromptLines(promptSection, "User", "UserPromptTemplateLines", contentRootPath),
            RequirementPromptTemplateLines = ReadPromptLines(promptSection, "Requirement", "RequirementPromptTemplateLines", contentRootPath),
            ContextChatPromptTemplateLines = ReadPromptLines(promptSection, "ContextChat", "ContextChatPromptTemplateLines", contentRootPath),
            ContextCompressionPromptTemplateLines = ReadPromptLines(promptSection, "ContextCompression", "ContextCompressionPromptTemplateLines", contentRootPath),
            NodeChatPromptTemplateLines = ReadPromptLines(promptSection, "NodeChat", "NodeChatPromptTemplateLines", contentRootPath),
            NodeChatCompressionPromptTemplateLines = ReadPromptLines(promptSection, "NodeChatCompression", "NodeChatCompressionPromptTemplateLines", contentRootPath),
            MapChatPromptTemplateLines = ReadPromptLines(promptSection, "MapChat", "MapChatPromptTemplateLines", contentRootPath),
            AppHelpPromptTemplateLines = ReadPromptLines(promptSection, "AppHelp", "AppHelpPromptTemplateLines", contentRootPath),
            AppManualLines = ReadPromptLines(promptSection, "AppManual", "AppManualLines", contentRootPath),
            AppManualPath = ResolveOptionalPromptFilePath(promptSection, "AppManual", contentRootPath),
            AppHelpLearningPath = ResolveOptionalPromptFilePath(promptSection, "AppHelpLearning", contentRootPath),
            AppHelpUsageTipsPath = ResolveOptionalPromptFilePath(promptSection, "AppHelpUsageTips", contentRootPath)
        }
    };
}

static AiAgentOptions LoadAiAgentOptions(IConfiguration configuration, string contentRootPath, string appBaseUrl)
{
    var section = configuration.GetSection("AiAgent");
    return new AiAgentOptions
    {
        AgentBuildPath = section["AgentBuildPath"] ?? @"G:\AAW+\NetMind\AgentBuild",
        PythonExecutable = section["PythonExecutable"] ?? "py",
        TimeoutSeconds = ReadInt(section["TimeoutSeconds"], 120),
        Temperature = ReadDouble(section["Temperature"], 0.2),
        MaxTokens = ReadInt(section["MaxTokens"], 4096),
        MaxRetries = ReadInt(section["MaxRetries"], 2),
        NetMindApiBaseUrl = appBaseUrl,
        SkillRuntimeTimeoutSeconds = ReadInt(section["SkillRuntimeTimeoutSeconds"], 10),
        NodeQuestion = ReadAiAgentScenarioOptions(
            section,
            "NodeIdentity",
            "NodeCues",
            contentRootPath),
        MapQuestion = ReadAiAgentScenarioOptions(
            section,
            "MapIdentity",
            "MapCues",
            contentRootPath),
        GlobalQuestion = ReadAiAgentScenarioOptions(
            section,
            "GlobalIdentity",
            "GlobalCues",
            contentRootPath),
        AppHelp = ReadAiAgentScenarioOptions(
            section,
            "AppHelpIdentity",
            "AppHelpCues",
            contentRootPath)
    };
}

static AiAgentScenarioOptions ReadAiAgentScenarioOptions(
    IConfigurationSection section,
    string identityFileKey,
    string cuesFileKey,
    string contentRootPath)
{
    return new AiAgentScenarioOptions
    {
        DomainAndSkillBinding = "netmind",
        IdentityLines = ReadRequiredPromptFileLines(section, identityFileKey, contentRootPath),
        CuesLines = ReadRequiredPromptFileLines(section, cuesFileKey, contentRootPath)
    };
}

static IReadOnlyList<string> ReadPromptLines(
    IConfigurationSection promptSection,
    string fileKey,
    string legacyLinesKey,
    string contentRootPath)
{
    var promptFile = promptSection.GetSection("PromptFiles")[fileKey];
    if (!string.IsNullOrWhiteSpace(promptFile))
    {
        var filePath = ResolvePromptFilePath(contentRootPath, promptFile);
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"AI Prompt 文件不存在：{filePath}");
        }

        return File.ReadAllLines(filePath);
    }

    return promptSection.GetSection(legacyLinesKey).GetChildren()
        .Select(section => section.Value ?? string.Empty)
        .ToList();
}

static IReadOnlyList<string> ReadRequiredPromptFileLines(
    IConfigurationSection section,
    string fileKey,
    string contentRootPath)
{
    var promptFile = section.GetSection("PromptFiles")[fileKey];
    if (string.IsNullOrWhiteSpace(promptFile))
    {
        throw new InvalidOperationException($"必须配置 AiAgent:PromptFiles:{fileKey}。");
    }

    var filePath = ResolvePromptFilePath(contentRootPath, promptFile);
    if (!File.Exists(filePath))
    {
        throw new InvalidOperationException($"AI Prompt 文件不存在：{filePath}");
    }

    return File.ReadAllLines(filePath);
}

static string ResolvePromptFilePath(string contentRootPath, string promptFile)
{
    if (Path.IsPathRooted(promptFile))
    {
        return promptFile;
    }

    var contentRootCandidate = Path.GetFullPath(Path.Combine(contentRootPath, promptFile));
    if (File.Exists(contentRootCandidate))
    {
        return contentRootCandidate;
    }

    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, promptFile));
}

static string ResolveOptionalPromptFilePath(
    IConfigurationSection promptSection,
    string fileKey,
    string contentRootPath)
{
    var promptFile = promptSection.GetSection("PromptFiles")[fileKey];
    return string.IsNullOrWhiteSpace(promptFile)
        ? string.Empty
        : ResolvePromptFilePath(contentRootPath, promptFile);
}

static bool ReadBool(string? value)
{
    return bool.TryParse(value, out var result) && result;
}

static int ReadInt(string? value, int fallback)
{
    return int.TryParse(value, out var result) ? result : fallback;
}

static double ReadDouble(string? value, double fallback)
{
    return double.TryParse(value, out var result) ? result : fallback;
}
