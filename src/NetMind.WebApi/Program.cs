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

if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) &&
    string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5119", "http://[::]:5119");
}

builder.Services.AddControllers();
builder.Services.AddSingleton<IAppLogger, AppLogger>();
builder.Services.AddSingleton<IProjectStatusRepository, ProjectStatusRepository>();
builder.Services.AddSingleton(_ => new PostgresConnectionFactory(
builder.Configuration.GetConnectionString("Postgres") ?? string.Empty));
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
builder.Services.AddHttpClient<IAiCleanService, AiCleanService>();
builder.Services.AddSingleton(LoadAiCleanOptions(builder.Configuration));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    StartFrontendDevServer(app);
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

static void StartFrontendDevServer(WebApplication app)
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

static AiCleanOptions LoadAiCleanOptions(IConfiguration configuration)
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
            SystemPromptLines = promptSection.GetSection("SystemPromptLines").GetChildren()
                .Select(section => section.Value ?? string.Empty)
                .ToList(),
            UserPromptTemplateLines = promptSection.GetSection("UserPromptTemplateLines").GetChildren()
                .Select(section => section.Value ?? string.Empty)
                .ToList(),
            RequirementPromptTemplateLines = promptSection.GetSection("RequirementPromptTemplateLines").GetChildren()
                .Select(section => section.Value ?? string.Empty)
                .ToList(),
            ContextChatPromptTemplateLines = promptSection.GetSection("ContextChatPromptTemplateLines").GetChildren()
                .Select(section => section.Value ?? string.Empty)
                .ToList(),
            ContextCompressionPromptTemplateLines = promptSection.GetSection("ContextCompressionPromptTemplateLines").GetChildren()
                .Select(section => section.Value ?? string.Empty)
                .ToList()
        }
    };
}

static bool ReadBool(string? value)
{
    return bool.TryParse(value, out var result) && result;
}

static int ReadInt(string? value, int fallback)
{
    return int.TryParse(value, out var result) ? result : fallback;
}
