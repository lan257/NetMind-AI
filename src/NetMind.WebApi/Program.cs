using Microsoft.Extensions.FileProviders;
using NetMind.Repository.Implementations;
using NetMind.Repository.Interfaces;
using NetMind.Services.Implementations;
using NetMind.Services.Interfaces;
using NetMind.WebApi.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IProjectStatusRepository, ProjectStatusRepository>();
builder.Services.AddSingleton<InMemoryMindMapStore>();
builder.Services.AddScoped<IMindMapRepository, MindMapRepository>();
builder.Services.AddScoped<INodeRepository, NodeRepository>();
builder.Services.AddScoped<INodeRelationRepository, NodeRelationRepository>();
builder.Services.AddScoped<IProjectStatusService, ProjectStatusService>();
builder.Services.AddScoped<IMindMapService, MindMapService>();
builder.Services.AddScoped<INodeService, NodeService>();
builder.Services.AddScoped<INodeRelationService, NodeRelationService>();

var app = builder.Build();

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
