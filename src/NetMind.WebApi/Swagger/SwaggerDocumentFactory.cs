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
                description = "NetMind P1.4 导图、导入导出、AI 清洗、日志和 AI 对话记录接口。"
            },
            paths = new Dictionary<string, object>
            {
                ["/api/mind-maps"] = Path("查询和创建导图", "get", "post"),
                ["/api/mind-maps/{id}"] = Path("查询、更新和逻辑删除单个导图", "get", "put", "delete"),
                ["/api/mind-maps/{id}/cascade"] = Path("逻辑删除导图及其节点和关联", "delete"),
                ["/api/nodes/by-map/{mapId}"] = Path("查询导图内节点", "get"),
                ["/api/nodes/{id}"] = Path("查询、更新和逻辑删除单个节点", "get", "put", "delete"),
                ["/api/nodes/{id}/subtree"] = Path("逻辑删除节点及其子树", "delete"),
                ["/api/node-relations/by-map/{mapId}"] = Path("查询导图内节点关联", "get"),
                ["/api/node-relations/{id}"] = Path("查询、更新和逻辑删除单个节点关联", "get", "put", "delete"),
                ["/api/node-relations/by-node/{nodeId}"] = Path("按节点逻辑删除相关关联", "delete"),
                ["/api/mind-map-transfer/{mapId}/structure"] = Path("导出完整导图结构", "get"),
                ["/api/mind-map-transfer/{mapId}/file"] = Path("导出完整导图 JSON 文件", "get"),
                ["/api/mind-map-transfer/structure"] = Path("从结构体导入完整导图", "post"),
                ["/api/mind-map-transfer/file"] = Path("从上传的 JSON 文件导入完整导图", "post"),
                ["/api/mind-map-transfer/template"] = Path("下载 JSON 导入模板", "get"),
                ["/api/ai/models"] = Path("查询 AI 清洗模型配置", "get"),
                ["/api/ai/clean"] = Path("通过 DeepSeek 或 Ollama 将自然语言清洗为标准导图结构", "post"),
                ["/api/ai/context-chat"] = Path("基于当前对话上下文与 AI 对话", "post"),
                ["/api/ai/requirements/structure"] = Path("结合上下文压缩拆解不成熟需求", "post"),
                ["/api/ai/node-chat"] = Path("基于当前节点上下文问答", "post"),
                ["/api/ai/node-agent-chat"] = Path("调用 AgentBuild 进行节点问答 Agent 对话", "post"),
                ["/api/ai/map-chat"] = Path("基于当前思维导图全量结构问答", "post"),
                ["/api/ai/map-agent-chat"] = Path("调用 AgentBuild 进行全图问答 Agent 对话", "post"),
                ["/api/ai/global-agent-chat"] = Path("调用 AgentBuild 进行全局问答 Agent 对话", "post"),
                ["/api/ai/app-help-agent-chat"] = Path("调用 AgentBuild 进行应用帮助 Agent 对话", "post"),
                ["/api/ai/app-help-chat"] = Path("基于应用帮助文档问答", "post"),
                ["/api/ai-conversation-records"] = Path("查询和创建 AI 对话记录", "get", "post"),
                ["/api/ai-conversation-records/{id}"] = Path("查询、更新和逻辑删除单条 AI 对话记录", "get", "put", "delete"),
                ["/api/system/health"] = Path("查询系统健康状态", "get"),
                ["/api/system/crypto/api-key-public-key"] = Path("查询 API Key 加密公钥", "get")
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
                <title>NetMind 接口文档</title>
                <style>
                    body { font-family: Arial, sans-serif; margin: 32px; color: #1f2937; }
                    code { background: #f3f4f6; padding: 2px 6px; border-radius: 4px; }
                    li { margin: 8px 0; }
                </style>
            </head>
            <body>
                <h1>NetMind 接口文档</h1>
                <p>OpenAPI JSON：<a href="/swagger/v1/swagger.json">/swagger/v1/swagger.json</a></p>
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
                    ["200"] = new { description = "成功" },
                    ["400"] = new { description = "请求错误" },
                    ["404"] = new { description = "未找到" }
                }
            });
    }
}
