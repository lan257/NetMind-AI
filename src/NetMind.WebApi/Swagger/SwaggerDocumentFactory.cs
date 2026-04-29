namespace NetMind.WebApi.Swagger;

internal static class SwaggerDocumentFactory
{
    public static object Create()
    {
        return new
        {
            openapi = "3.0.1",
            info = new
            {
                title = "NetMind API",
                version = "v1",
                description = "Mind map CRUD, import/export and P1.1 AI cleaning API."
            },
            paths = new Dictionary<string, object>
            {
                ["/api/mind-maps"] = Path("List and create mind maps", "get", "post"),
                ["/api/mind-maps/{id}"] = Path("Get, update and logically delete one mind map", "get", "put", "delete"),
                ["/api/mind-maps/{id}/cascade"] = Path("Logically delete one mind map with its nodes and relations", "delete"),
                ["/api/nodes/by-map/{mapId}"] = Path("List nodes in a mind map", "get"),
                ["/api/nodes/{id}"] = Path("Get, update and logically delete one node only", "get", "put", "delete"),
                ["/api/nodes/{id}/subtree"] = Path("Logically delete one node and its subtree", "delete"),
                ["/api/node-relations/by-map/{mapId}"] = Path("List node relations in a mind map", "get"),
                ["/api/node-relations/{id}"] = Path("Get, update and logically delete one node relation", "get", "put", "delete"),
                ["/api/node-relations/by-node/{nodeId}"] = Path("Logically delete all relations connected to one node", "delete"),
                ["/api/mind-map-transfer/{mapId}/structure"] = Path("Export one full mind map as a structured response", "get"),
                ["/api/mind-map-transfer/{mapId}/file"] = Path("Export one full mind map as a JSON file", "get"),
                ["/api/mind-map-transfer/structure"] = Path("Import one full mind map from a structured request", "post"),
                ["/api/mind-map-transfer/file"] = Path("Import one full mind map from an uploaded JSON file", "post"),
                ["/api/mind-map-transfer/template"] = Path("Download a JSON import template", "get"),
                ["/api/ai/models"] = Path("List configured AI model placeholders", "get"),
                ["/api/ai/clean"] = Path("Clean natural language into the standard mind map transfer structure", "post"),
                ["/api/system/health"] = Path("Get system health", "get")
            }
        };
    }

    public static string CreateHtml()
    {
        return """
            <!doctype html>
            <html lang="zh-CN">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>NetMind Swagger</title>
                <style>
                    body { font-family: Arial, sans-serif; margin: 32px; color: #1f2937; }
                    code { background: #f3f4f6; padding: 2px 6px; border-radius: 4px; }
                    li { margin: 8px 0; }
                </style>
            </head>
            <body>
                <h1>NetMind Swagger</h1>
                <p>OpenAPI JSON: <a href="/swagger/v1/swagger.json">/swagger/v1/swagger.json</a></p>
                <ul id="paths"></ul>
                <script>
                    fetch('/swagger/v1/swagger.json')
                        .then(response => response.json())
                        .then(doc => {
                            const list = document.getElementById('paths');
                            Object.entries(doc.paths).forEach(([path, methods]) => {
                                Object.keys(methods).forEach(method => {
                                    const item = document.createElement('li');
                                    item.innerHTML = '<code>' + method.toUpperCase() + '</code> ' + path;
                                    list.appendChild(item);
                                });
                            });
                        });
                </script>
            </body>
            </html>
            """;
    }

    private static Dictionary<string, object> Path(string summary, params string[] methods)
    {
        return methods.ToDictionary(
            method => method,
            method => (object)new
            {
                summary,
                responses = new Dictionary<string, object>
                {
                    ["200"] = new { description = "OK" },
                    ["400"] = new { description = "Bad Request" },
                    ["404"] = new { description = "Not Found" }
                }
            });
    }
}
