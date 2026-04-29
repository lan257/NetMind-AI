using Microsoft.Extensions.FileProviders;
using NetMind.Common.Responses;
using NetMind.Repository.Implementations;
using NetMind.Repository.Interfaces;
using NetMind.Services.Configurations;
using NetMind.Services.Implementations;
using NetMind.Services.Interfaces;
using NetMind.WebApi.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IProjectStatusRepository, ProjectStatusRepository>();
builder.Services.AddSingleton(_ => new PostgresConnectionFactory(
builder.Configuration.GetConnectionString("Postgres") ?? string.Empty));
builder.Services.AddScoped<IMindMapRepository, MindMapRepository>();
builder.Services.AddScoped<INodeRepository, NodeRepository>();
builder.Services.AddScoped<INodeRelationRepository, NodeRelationRepository>();
builder.Services.AddScoped<IProjectStatusService, ProjectStatusService>();
builder.Services.AddScoped<IMindMapService, MindMapService>();
builder.Services.AddScoped<INodeService, NodeService>();
builder.Services.AddScoped<INodeRelationService, NodeRelationService>();
builder.Services.AddScoped<IMindMapTransferService, MindMapTransferService>();
builder.Services.AddHttpClient<IAiCleanService, AiCleanService>();
builder.Services.AddSingleton(LoadAiCleanOptions(builder.Configuration));

var app = builder.Build();

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
            ? $"Database request failed: {ex.Message}"
            : ex.Message;
        await context.Response.WriteAsJsonAsync(ApiResult<object>.Fail(message));
    }
});

app.UseRouting();

var frontendDistRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "NetMind.Frontend", "dist"));
if (Directory.Exists(frontendDistRoot))
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

app.Run();

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
            SystemPromptLines = promptSection.GetSection("SystemPromptLines").GetChildren()
                .Select(section => section.Value ?? string.Empty)
                .ToList(),
            UserPromptTemplateLines = promptSection.GetSection("UserPromptTemplateLines").GetChildren()
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
