using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NetMind.Common.Logging;
using NetMind.Models.Dtos;
using NetMind.Repository.Interfaces;
using NetMind.Services.Configurations;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed class AiCleanService : IAiCleanService
{
    private const string SchemaVersion = "netmind.mindmap.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AiCleanOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IAppLogger _logger;
    private readonly INodeRepository _nodeRepository;
    private readonly INodeRelationRepository _relationRepository;
    private readonly string _systemPrompt;
    private readonly string _userPromptTemplate;
    private readonly string _requirementPromptTemplate;
    private readonly string _contextChatPromptTemplate;
    private readonly string _contextCompressionPromptTemplate;
    private readonly string _nodeChatPromptTemplate;
    private readonly string _nodeChatCompressionPromptTemplate;

    public AiCleanService(AiCleanOptions options, HttpClient httpClient, IAppLogger logger,
        INodeRepository nodeRepository, INodeRelationRepository relationRepository)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;
        _nodeRepository = nodeRepository;
        _relationRepository = relationRepository;
        _systemPrompt = JoinPromptLines(options.Prompt.SystemPromptLines, "AiClean:Prompt:SystemPromptLines");
        _userPromptTemplate = JoinPromptLines(options.Prompt.UserPromptTemplateLines, "AiClean:Prompt:UserPromptTemplateLines");
        _requirementPromptTemplate = JoinPromptLines(options.Prompt.RequirementPromptTemplateLines, "AiClean:Prompt:RequirementPromptTemplateLines");
        _contextChatPromptTemplate = JoinPromptLines(options.Prompt.ContextChatPromptTemplateLines, "AiClean:Prompt:ContextChatPromptTemplateLines");
        _contextCompressionPromptTemplate = JoinPromptLines(options.Prompt.ContextCompressionPromptTemplateLines, "AiClean:Prompt:ContextCompressionPromptTemplateLines");
        _nodeChatPromptTemplate = JoinPromptLines(options.Prompt.NodeChatPromptTemplateLines, "AiClean:Prompt:NodeChatPromptTemplateLines");
        _nodeChatCompressionPromptTemplate = JoinPromptLines(options.Prompt.NodeChatCompressionPromptTemplateLines, "AiClean:Prompt:NodeChatCompressionPromptTemplateLines");
    }

    public IReadOnlyList<AiModelOptionDto> ListModels()
    {
        return _options.Models
            .OrderByDescending(model => model.IsDefault)
            .ThenBy(model => model.Provider.Equals("deepseek", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(model => model.Id, StringComparer.Ordinal)
            .Select(model => new AiModelOptionDto
            {
                Id = model.Id,
                Name = model.Name,
                Provider = model.Provider,
                Endpoint = model.Endpoint,
                IsDefault = model.IsDefault,
                Status = model.Enabled ? "enabled" : "disabled",
                Notes = model.Notes
            })
            .ToList();
    }

    public async Task<AiCleanResultDto> CleanAsync(AiCleanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NaturalLanguage))
        {
            throw new ArgumentException("请输入自然语言内容。", nameof(request));
        }

        var prompt = BuildUserPrompt(request.NaturalLanguage);
        var candidates = SelectModels(request.ModelId);
        Exception? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                var content = await CallModelAsync(candidate, prompt, request.ApiKey);

                var transfer = ParseTransfer(content);
                ValidateTransfer(transfer);

                return new AiCleanResultDto
                {
                    SelectedModel = ToDto(candidate),
                    Prompt = prompt,
                    Transfer = transfer,
                    Warnings = lastError is null
                        ? Array.Empty<string>()
                        : new[] { $"主模型调用失败，已使用备用模型：{lastError.Message}" }
                };
            }
            catch (Exception ex) when (string.IsNullOrWhiteSpace(request.ModelId) && ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("未配置可用的 AI 清洗模型。");
    }

    public async Task<AiContextChatResultDto> ChatWithContextAsync(AiContextChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("请输入对话内容。", nameof(request));
        }

        var candidates = SelectModels(request.ModelId);
        Exception? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                var contextResult = await CompressContextIfNeededAsync(candidate, request.Context, request.ApiKey);
                var prompt = BuildContextChatPrompt(request.Message, contextResult.Context);
                var content = await CallModelAsync(candidate, prompt, request.ApiKey);
                var reply = ParseContextChatReply(content);

                return new AiContextChatResultDto
                {
                    SelectedModel = ToDto(candidate),
                    Prompt = prompt,
                    Reply = reply,
                    ContextSummary = contextResult.Context,
                    WasContextCompressed = contextResult.WasCompressed,
                    Warnings = lastError is null
                        ? Array.Empty<string>()
                        : new[] { $"主模型调用失败，已使用备用模型：{lastError.Message}" }
                };
            }
            catch (Exception ex) when (string.IsNullOrWhiteSpace(request.ModelId) && ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("未配置可用的 AI 清洗模型。");
    }

    public async Task<AiRequirementStructureResultDto> StructureRequirementAsync(AiRequirementStructureRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Requirement))
        {
            throw new ArgumentException("请输入需求内容。", nameof(request));
        }

        var candidates = SelectModels(request.ModelId);
        Exception? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                var contextResult = await CompressContextIfNeededAsync(candidate, request.Context, request.ApiKey);
                var prompt = BuildRequirementPrompt(request.Requirement, contextResult.Context);
                var content = await CallModelAsync(candidate, prompt, request.ApiKey);
                var transfer = ParseTransfer(content);
                ValidateTransfer(transfer);

                return new AiRequirementStructureResultDto
                {
                    SelectedModel = ToDto(candidate),
                    Prompt = prompt,
                    ContextSummary = contextResult.Context,
                    WasContextCompressed = contextResult.WasCompressed,
                    Transfer = transfer,
                    Warnings = lastError is null
                        ? Array.Empty<string>()
                        : new[] { $"主模型调用失败，已使用备用模型：{lastError.Message}" }
                };
            }
            catch (Exception ex) when (string.IsNullOrWhiteSpace(request.ModelId) && ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("未配置可用的 AI 清洗模型。");
    }

    public async Task<AiNodeChatResult> ChatWithNodeAsync(AiNodeChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("请输入对话内容。", nameof(request));
        }

        if (request.NodeId <= 0)
        {
            throw new ArgumentException("请选择有效的节点。", nameof(request));
        }

        var node = await _nodeRepository.GetAsync(request.NodeId);
        if (node is null)
        {
            throw new ArgumentException("节点不存在。", nameof(request));
        }

        var nodeContext = await BuildNodeContextAsync(node);

        var maxLength = Math.Max(request.MaxContextLength, 1024);
        var contextText = request.Context?.Trim() ?? string.Empty;
        var usagePercent = maxLength <= 0 ? 0 : (double)contextText.Length / maxLength * 100;

        string contextStatus;
        if (usagePercent > 100)
        {
            throw new InvalidOperationException($"当前上下文长度为 {contextText.Length} 字符，超过上限 {maxLength} 字符（{usagePercent:F0}%），请删减上下文或分多次发送。");
        }

        string prompt;
        bool needCompression;
        int compressionTargetLength = 0;
        int maxReplyLength = maxLength;

        if (usagePercent > 80)
        {
            contextStatus = "critical";
            needCompression = false;
            contextText = string.Empty;
            prompt = BuildNodeChatPrompt(nodeContext, contextText, request.Message, false, 0, maxReplyLength);
        }
        else if (usagePercent > 60)
        {
            contextStatus = "warning";
            needCompression = true;
            compressionTargetLength = (int)(maxLength * 0.4);
            maxReplyLength = (int)(maxLength * 0.4);
            prompt = BuildNodeChatPrompt(nodeContext, contextText, request.Message, true, compressionTargetLength, maxReplyLength);
        }
        else
        {
            contextStatus = "comfortable";
            needCompression = false;
            prompt = BuildNodeChatPrompt(nodeContext, contextText, request.Message, false, 0, maxReplyLength);
        }

        var candidates = SelectModels(request.ModelId);
        Exception? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                var content = await CallModelForTextAsync(candidate, prompt, request.ApiKey);
                var (reply, compressedContext) = ParseNodeChatResponse(content);

                return new AiNodeChatResult
                {
                    SelectedModel = ToDto(candidate),
                    Prompt = prompt,
                    Reply = reply,
                    CompressedContext = compressedContext,
                    WasContextCompressed = needCompression && !string.IsNullOrWhiteSpace(compressedContext),
                    ContextUsagePercent = usagePercent,
                    ContextStatus = contextStatus,
                    Warnings = lastError is null
                        ? Array.Empty<string>()
                        : new[] { $"主模型调用失败，已使用备用模型：{lastError.Message}" }
                };
            }
            catch (Exception ex) when (string.IsNullOrWhiteSpace(request.ModelId) && ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("未配置可用的 AI 清洗模型。");
    }

    private async Task<string> BuildNodeContextAsync(NetMind.Models.Entities.NodeEntity node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"标题: {node.Title}");
        if (!string.IsNullOrWhiteSpace(node.Content))
        {
            sb.AppendLine($"内容: {node.Content}");
        }

        var allNodes = await _nodeRepository.ListByMapAsync(node.MapId);

        var parents = new List<string>();
        var current = node.ParentId.HasValue
            ? allNodes.FirstOrDefault(n => n.Id == node.ParentId.Value)
            : null;
        while (current is not null)
        {
            parents.Insert(0, current.Title);
            current = current.ParentId.HasValue
                ? allNodes.FirstOrDefault(n => n.Id == current.ParentId.Value)
                : null;
        }
        sb.AppendLine($"父节点链: {(parents.Count > 0 ? string.Join(" > ", parents) : "(根节点)")}");

        var children = allNodes.Where(n => n.ParentId == node.Id).ToList();
        sb.AppendLine($"子节点({children.Count}): {(children.Count > 0 ? string.Join(", ", children.Take(10).Select(c => c.Title)) : "(无)")}");

        var relations = await _relationRepository.ListByNodeAsync(node.Id);
        var relationDescriptions = new List<string>();
        foreach (var rel in relations)
        {
            var isSource = rel.SourceId == node.Id;
            var otherId = isSource ? rel.TargetId : rel.SourceId;
            var otherNode = allNodes.FirstOrDefault(n => n.Id == otherId);
            var otherTitle = otherNode?.Title ?? $"节点#{otherId}";
            var direction = isSource ? "→" : "←";
            relationDescriptions.Add($"{direction} [{rel.RelationType}] {otherTitle}");
        }
        sb.AppendLine($"关联节点({relations.Count}): {(relationDescriptions.Count > 0 ? string.Join("; ", relationDescriptions) : "(无)")}");

        return sb.ToString();
    }

    private string BuildNodeChatPrompt(string nodeContext, string contextText, string message,
        bool needCompression, int compressionTargetLength, int maxReplyLength)
    {
        if (needCompression)
        {
            return _nodeChatCompressionPromptTemplate
                .Replace("{{nodeContext}}", nodeContext, StringComparison.Ordinal)
                .Replace("{{context}}", contextText, StringComparison.Ordinal)
                .Replace("{{message}}", message.Trim(), StringComparison.Ordinal)
                .Replace("{{compressionTargetLength}}", compressionTargetLength.ToString(), StringComparison.Ordinal)
                .Replace("{{maxReplyLength}}", maxReplyLength.ToString(), StringComparison.Ordinal);
        }

        return _nodeChatPromptTemplate
            .Replace("{{nodeContext}}", nodeContext, StringComparison.Ordinal)
            .Replace("{{context}}", string.IsNullOrWhiteSpace(contextText) ? "（无历史上下文）" : contextText, StringComparison.Ordinal)
            .Replace("{{message}}", message.Trim(), StringComparison.Ordinal);
    }

    private static (string Reply, string CompressedContext) ParseNodeChatResponse(string content)
    {
        var text = StripMarkdownFence(content.Trim());

        var replyStart = text.IndexOf("【正文】", StringComparison.Ordinal);
        var ctxStart = text.IndexOf("【压缩上下文】", StringComparison.Ordinal);

        string reply;
        string compressedContext;

        if (replyStart >= 0)
        {
            var replyEnd = ctxStart > replyStart ? ctxStart : text.Length;
            reply = text[(replyStart + 4)..replyEnd].Trim();
        }
        else
        {
            reply = text;
        }

        if (ctxStart >= 0)
        {
            var ctxContent = text[(ctxStart + 7)..].Trim();
            compressedContext = ctxContent.Length > 0 && ctxContent != "（空）" && ctxContent != "(空)" ? ctxContent : string.Empty;
        }
        else
        {
            compressedContext = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            throw new InvalidOperationException("AI 节点对话返回正文为空。");
        }

        return (reply, compressedContext);
    }

    private IReadOnlyList<AiModelOptions> SelectModels(string? requestedModelId)
    {
        if (!string.IsNullOrWhiteSpace(requestedModelId))
        {
            var requested = _options.Models.FirstOrDefault(model =>
                model.Enabled && string.Equals(model.Id, requestedModelId, StringComparison.Ordinal));
            if (requested is not null)
            {
                return new[] { requested };
            }

            throw new ArgumentException($"AI 模型 '{requestedModelId}' 未配置或未启用。", nameof(requestedModelId));
        }

        return _options.Models
            .Where(model => model.Enabled)
            .OrderBy(model => model.IsDefault ? 0 : 1)
            .ThenBy(model => model.Provider.Equals("deepseek", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(model => model.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(model => model.Id, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<string> CallOpenAiCompatibleAsync(AiModelOptions model, string prompt, string? apiKeyOverride = null)
    {
        return await CallOpenAiCompatibleInternalAsync(model, prompt, apiKeyOverride, true);
    }

    private async Task<string> CallOpenAiCompatibleTextAsync(AiModelOptions model, string prompt, string? apiKeyOverride = null)
    {
        return await CallOpenAiCompatibleInternalAsync(model, prompt, apiKeyOverride, false);
    }

    private async Task<string> CallOpenAiCompatibleInternalAsync(AiModelOptions model, string prompt, string? apiKeyOverride, bool jsonResponse)
    {
        var stopwatch = Stopwatch.StartNew();
        using var request = new HttpRequestMessage(HttpMethod.Post, model.Endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var apiKey = apiKeyOverride ?? ResolveApiKey(model);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"AI 模型 '{model.Id}' 需要配置 API Key。");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var body = new Dictionary<string, object>
        {
            ["model"] = model.Model,
            ["messages"] = new[]
            {
                new { role = "system", content = _systemPrompt },
                new { role = "user", content = prompt }
            },
            ["temperature"] = 0.2
        };

        if (jsonResponse)
        {
            body["response_format"] = new { type = "json_object" };
        }

        request.Content = JsonContent(body);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(model.TimeoutSeconds, 1)));
        using var response = await _httpClient.SendAsync(request, timeout.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);
        stopwatch.Stop();
        LogAiApiCall(model, response.StatusCode, stopwatch.ElapsedMilliseconds);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI 模型 '{model.Id}' 请求失败：{(int)response.StatusCode} {response.ReasonPhrase}。");
        }

        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? throw new InvalidOperationException($"AI 模型 '{model.Id}' 返回内容为空。");
    }

    private async Task<string> CallOllamaAsync(AiModelOptions model, string prompt, string? apiKeyOverride = null)
    {
        return await CallOllamaInternalAsync(model, prompt, apiKeyOverride, true);
    }

    private async Task<string> CallOllamaTextAsync(AiModelOptions model, string prompt, string? apiKeyOverride = null)
    {
        return await CallOllamaInternalAsync(model, prompt, apiKeyOverride, false);
    }

    private async Task<string> CallOllamaInternalAsync(AiModelOptions model, string prompt, string? apiKeyOverride, bool jsonResponse)
    {
        var stopwatch = Stopwatch.StartNew();
        using var request = new HttpRequestMessage(HttpMethod.Post, model.Endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new Dictionary<string, object>
        {
            ["model"] = model.Model,
            ["stream"] = false,
            ["messages"] = new[]
            {
                new { role = "system", content = _systemPrompt },
                new { role = "user", content = prompt }
            },
            ["options"] = new { temperature = 0.2 }
        };

        if (jsonResponse)
        {
            body["format"] = "json";
        }

        request.Content = JsonContent(body);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(model.TimeoutSeconds, 1)));
        using var response = await _httpClient.SendAsync(request, timeout.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);
        stopwatch.Stop();
        LogAiApiCall(model, response.StatusCode, stopwatch.ElapsedMilliseconds);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI 模型 '{model.Id}' 请求失败：{(int)response.StatusCode} {response.ReasonPhrase}。");
        }

        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? throw new InvalidOperationException($"AI 模型 '{model.Id}' 返回内容为空。");
    }

    private Task<string> CallModelAsync(AiModelOptions model, string prompt, string? apiKeyOverride = null)
    {
        return model.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase)
            ? CallOllamaAsync(model, prompt, apiKeyOverride)
            : CallOpenAiCompatibleAsync(model, prompt, apiKeyOverride);
    }

    private Task<string> CallModelForTextAsync(AiModelOptions model, string prompt, string? apiKeyOverride = null)
    {
        return model.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase)
            ? CallOllamaTextAsync(model, prompt, apiKeyOverride)
            : CallOpenAiCompatibleTextAsync(model, prompt, apiKeyOverride);
    }

    private static MindMapTransferDto ParseTransfer(string content)
    {
        var json = StripMarkdownFence(content.Trim());
        var transfer = JsonSerializer.Deserialize<MindMapTransferDto>(json, JsonOptions);
        return transfer ?? throw new InvalidOperationException("AI 返回内容无法解析为导图结构体。");
    }

    private static string ParseContextChatReply(string content)
    {
        var value = StripMarkdownFence(content.Trim());
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("reply", out var reply))
        {
            throw new InvalidOperationException("AI 对话返回必须包含 reply 字段。");
        }

        return reply.GetString()?.Trim()
            ?? throw new InvalidOperationException("AI 对话返回的 reply 为空。");
    }

    private static void ValidateTransfer(MindMapTransferDto transfer)
    {
        if (!string.Equals(transfer.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"AI 返回的 schemaVersion 必须为 '{SchemaVersion}'。");
        }

        if (string.IsNullOrWhiteSpace(transfer.Title))
        {
            throw new InvalidOperationException("AI 返回内容必须包含标题。");
        }

        if (transfer.Nodes.Count == 0)
        {
            throw new InvalidOperationException("AI 返回内容至少需要包含一个节点。");
        }

        var clientIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in transfer.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.ClientId) || string.IsNullOrWhiteSpace(node.Title))
            {
                throw new InvalidOperationException("AI 返回的节点必须包含 clientId 和标题。");
            }

            if (!clientIds.Add(node.ClientId.Trim()))
            {
                throw new InvalidOperationException($"AI 返回内容包含重复节点 clientId：'{node.ClientId}'。");
            }
        }

        foreach (var node in transfer.Nodes.Where(node => !string.IsNullOrWhiteSpace(node.ParentClientId)))
        {
            if (!clientIds.Contains(node.ParentClientId!.Trim()))
            {
                throw new InvalidOperationException($"AI 返回内容引用了不存在的父节点：'{node.ParentClientId}'。");
            }
        }

        foreach (var relation in transfer.Relations)
        {
            if (string.IsNullOrWhiteSpace(relation.SourceClientId) || string.IsNullOrWhiteSpace(relation.TargetClientId))
            {
                throw new InvalidOperationException("AI 返回的关联必须包含源端点和目标端点。");
            }

            if (!clientIds.Contains(relation.SourceClientId.Trim()) || !clientIds.Contains(relation.TargetClientId.Trim()))
            {
                throw new InvalidOperationException("AI 返回的关联端点必须存在于节点列表中。");
            }

            if (string.Equals(relation.SourceClientId.Trim(), relation.TargetClientId.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("AI 返回的关联源节点和目标节点不能相同。");
            }

            if (string.IsNullOrWhiteSpace(relation.RelationType) || relation.Weight < 0)
            {
                throw new InvalidOperationException("AI 返回的关联类型不能为空，权重不能为负数。");
            }
        }
    }

    private string BuildUserPrompt(string naturalLanguage)
    {
        return _userPromptTemplate
            .Replace("{{schemaVersion}}", SchemaVersion, StringComparison.Ordinal)
            .Replace("{{naturalLanguage}}", naturalLanguage.Trim(), StringComparison.Ordinal);
    }

    private string BuildRequirementPrompt(string requirement, string context)
    {
        return _requirementPromptTemplate
            .Replace("{{schemaVersion}}", SchemaVersion, StringComparison.Ordinal)
            .Replace("{{requirement}}", requirement.Trim(), StringComparison.Ordinal)
            .Replace("{{context}}", string.IsNullOrWhiteSpace(context) ? "No additional context." : context.Trim(), StringComparison.Ordinal);
    }

    private string BuildContextChatPrompt(string message, string context)
    {
        return _contextChatPromptTemplate
            .Replace("{{message}}", message.Trim(), StringComparison.Ordinal)
            .Replace("{{context}}", string.IsNullOrWhiteSpace(context) ? "No previous conversation." : context.Trim(), StringComparison.Ordinal);
    }

    private async Task<(string Context, bool WasCompressed)> CompressContextIfNeededAsync(AiModelOptions model, string? context, string? apiKeyOverride = null)
    {
        var trimmed = context?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return (string.Empty, false);
        }

        if (trimmed.Length <= Math.Max(_options.Prompt.ContextCompressionThreshold, 1))
        {
            return (trimmed, false);
        }

        var prompt = _contextCompressionPromptTemplate
            .Replace("{{context}}", trimmed, StringComparison.Ordinal);
        var compressed = ParseContextSummary(await CallModelAsync(model, prompt, apiKeyOverride));
        if (string.IsNullOrWhiteSpace(compressed))
        {
            throw new InvalidOperationException($"AI 模型 '{model.Id}' 返回的上下文摘要为空。");
        }

        return (compressed, true);
    }

    private static string ParseContextSummary(string content)
    {
        var value = StripMarkdownFence(content.Trim());
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("summary", out var summary))
            {
                return summary.GetString()?.Trim() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return value;
        }

        return value;
    }

    private static string JoinPromptLines(IReadOnlyList<string> lines, string configPath)
    {
        var cleaned = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (cleaned.Length == 0)
        {
            throw new InvalidOperationException($"必须配置 {configPath}。");
        }

        return string.Join("\n", cleaned);
    }

    private static StringContent JsonContent(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static string? ResolveApiKey(AiModelOptions model)
    {
        if (!string.IsNullOrWhiteSpace(model.ApiKey))
        {
            return model.ApiKey;
        }

        return string.IsNullOrWhiteSpace(model.ApiKeyEnvironmentVariable)
            ? null
            : Environment.GetEnvironmentVariable(model.ApiKeyEnvironmentVariable);
    }

    private static string StripMarkdownFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        var firstLineEnd = value.IndexOf('\n', StringComparison.Ordinal);
        var lastFence = value.LastIndexOf("```", StringComparison.Ordinal);
        if (firstLineEnd < 0 || lastFence <= firstLineEnd)
        {
            return value;
        }

        return value[(firstLineEnd + 1)..lastFence].Trim();
    }

    private static AiModelOptionDto ToDto(AiModelOptions model)
    {
        return new AiModelOptionDto
        {
            Id = model.Id,
            Name = model.Name,
            Provider = model.Provider,
            Endpoint = model.Endpoint,
            IsDefault = model.IsDefault,
            Status = model.Enabled ? "enabled" : "disabled",
            Notes = model.Notes
        };
    }

    private void LogAiApiCall(AiModelOptions model, System.Net.HttpStatusCode statusCode, long elapsedMs)
    {
        _logger.Info("AI API 调用", "AI 模型接口调用完成。", new Dictionary<string, object?>
        {
            ["ModelId"] = model.Id,
            ["Provider"] = model.Provider,
            ["Endpoint"] = model.Endpoint,
            ["StatusCode"] = (int)statusCode,
            ["ElapsedMs"] = elapsedMs
        });
    }
}
