using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetMind.Common.Logging;
using NetMind.Models.Dtos;
using NetMind.Models.Entities;
using NetMind.Repository.Interfaces;
using NetMind.Services.Configurations;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed class AiAgentService : IAiAgentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AiAgentOptions _agentOptions;
    private readonly AiCleanOptions _aiOptions;
    private readonly INodeRepository _nodeRepository;
    private readonly INodeRelationRepository _relationRepository;
    private readonly IAppLogger _logger;

    public AiAgentService(
        AiAgentOptions agentOptions,
        AiCleanOptions aiOptions,
        INodeRepository nodeRepository,
        INodeRelationRepository relationRepository,
        IAppLogger logger)
    {
        _agentOptions = agentOptions;
        _aiOptions = aiOptions;
        _nodeRepository = nodeRepository;
        _relationRepository = relationRepository;
        _logger = logger;
    }

    public async Task<AiAgentChatResult> ChatWithNodeAgentAsync(AiNodeAgentChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message) && request.ConfirmedSkillCalls.Count == 0)
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

        var maxLength = Math.Max(request.MaxContextLength, 1024);
        var contextText = request.Context?.Trim() ?? string.Empty;
        var usagePercent = maxLength <= 0 ? 0 : (double)contextText.Length / maxLength * 100;
        if (usagePercent > 100)
        {
            throw new InvalidOperationException($"当前上下文长度为 {contextText.Length} 字符，超过上限 {maxLength} 字符（{usagePercent:F0}%），请删减上下文或分多次发送。");
        }

        var contextStatus = usagePercent > 80
            ? "critical"
            : usagePercent > 60
                ? "warning"
                : "comfortable";

        if (contextStatus == "critical")
        {
            contextText = string.Empty;
        }

        var scenario = _agentOptions.NodeQuestion;
        var selectedModel = ResolveAgentModel(request);
        var modelConfig = BuildModelConfig(selectedModel, request.ApiKey);
        var focusContext = await BuildNodeFocusContextAsync(node, contextText, maxLength, usagePercent, contextStatus);
        var agentContext = BuildAgentContext(request.AgentContext, focusContext);
        var kernelRoot = ResolveAgentBuildRoot(request.AgentBuildPath);
        var kernelRequest = BuildKernelRequest(request, scenario, modelConfig, agentContext);
        var promptForLog = BuildRedactedPayloadJson(kernelRequest);
        var kernelResponse = await RunKernelAsync(kernelRoot, kernelRequest);

        return new AiAgentChatResult
        {
            SelectedModel = ToDto(selectedModel),
            Prompt = promptForLog,
            Reply = string.IsNullOrWhiteSpace(kernelResponse.MainText)
                ? kernelResponse.Error ?? "Agent 未返回正文。"
                : kernelResponse.MainText,
            Status = kernelResponse.Status,
            AgentTarget = kernelResponse.AgentTarget,
            SkillCalls = CloneElements(kernelResponse.SkillCalls),
            ContextUpdate = kernelResponse.ContextUpdate.ValueKind == JsonValueKind.Undefined
                ? EmptyJsonObject()
                : kernelResponse.ContextUpdate.Clone(),
            ContextUsagePercent = usagePercent,
            ContextStatus = contextStatus,
            Warnings = Array.Empty<string>()
        };
    }

    private Dictionary<string, object?> BuildKernelRequest(
        AiNodeAgentChatRequest request,
        AiAgentScenarioOptions scenario,
        Dictionary<string, object?> modelConfig,
        Dictionary<string, object?> agentContext)
    {
        var domain = string.IsNullOrWhiteSpace(request.DomainAndSkillBinding)
            ? scenario.DomainAndSkillBinding
            : request.DomainAndSkillBinding.Trim();

        var userText = string.IsNullOrWhiteSpace(request.Message)
            ? "用户已处理上一轮 Agent Skill 权限，请继续完成任务。"
            : request.Message.Trim();

        return new Dictionary<string, object?>
        {
            ["conversation_id"] = string.IsNullOrWhiteSpace(request.ConversationId)
                ? $"node-agent-{Guid.NewGuid():N}"
                : request.ConversationId,
            ["user_text"] = userText,
            ["domain_and_skill_binding"] = string.IsNullOrWhiteSpace(domain) ? "default" : domain,
            ["identity"] = JoinLines(scenario.IdentityLines, "你是 NetMind 的节点问答 Agent。"),
            ["cues"] = JoinLines(scenario.CuesLines, "使用中文，围绕当前节点上下文回答。"),
            ["model_config"] = modelConfig,
            ["context"] = agentContext,
            ["confirmed_skill_calls"] = CloneElements(request.ConfirmedSkillCalls),
            ["history_skill_calls"] = CloneElements(request.HistorySkillCalls)
        };
    }

    private async Task<AgentKernelResponse> RunKernelAsync(string kernelRoot, Dictionary<string, object?> kernelRequest)
    {
        var payloadJson = JsonSerializer.Serialize(kernelRequest, JsonOptions);
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = _agentOptions.PythonExecutable,
            WorkingDirectory = kernelRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        process.StartInfo.ArgumentList.Add("-m");
        process.StartInfo.ArgumentList.Add("src.agent_kernel");
        process.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        process.StartInfo.Environment["PYTHONDONTWRITEBYTECODE"] = "1";

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("AgentBuild 内核进程启动失败。");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"AgentBuild 内核进程启动失败：{ex.Message}", ex);
        }

        await process.StandardInput.WriteAsync(payloadJson);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(_agentOptions.TimeoutSeconds, 1)));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException ex)
        {
            TryKill(process);
            throw new TimeoutException($"AgentBuild 内核执行超过 {_agentOptions.TimeoutSeconds} 秒。", ex);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        stopwatch.Stop();

        _logger.Info("AgentBuild 调用", "AgentBuild 内核进程执行完成。", new Dictionary<string, object?>
        {
            ["KernelRoot"] = kernelRoot,
            ["ExitCode"] = process.ExitCode,
            ["ElapsedMs"] = stopwatch.ElapsedMilliseconds
        });

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"AgentBuild 内核执行失败：{(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim()}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException("AgentBuild 内核未返回内容。");
        }

        var response = JsonSerializer.Deserialize<AgentKernelResponse>(stdout, JsonOptions);
        return response ?? throw new InvalidOperationException("AgentBuild 内核返回内容无法解析。");
    }

    private Dictionary<string, object?> BuildModelConfig(AiModelOptions model, string? apiKeyOverride)
    {
        var apiKey = apiKeyOverride ?? ResolveApiKey(model);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"AI 模型 '{model.Name}' 缺少 API Key。请在「设置 → AI 大模型配置」中为模型配置 API Key，" +
                $"或设置环境变量 {(string.IsNullOrWhiteSpace(model.ApiKeyEnvironmentVariable) ? "" : model.ApiKeyEnvironmentVariable)}。");
        }

        return new Dictionary<string, object?>
        {
            ["model_name"] = model.Model,
            ["api_url"] = model.Endpoint,
            ["api_key"] = apiKey,
            ["temperature"] = _agentOptions.Temperature,
            ["max_tokens"] = _agentOptions.MaxTokens,
            ["timeout"] = Math.Max(model.TimeoutSeconds, 1),
            ["max_retries"] = _agentOptions.MaxRetries,
            ["response_format"] = new Dictionary<string, object?> { ["type"] = "json_object" }
        };
    }

    private AiModelOptions ResolveAgentModel(AiAgentChatRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ModelId))
        {
            var configured = _aiOptions.Models.FirstOrDefault(model =>
                model.Enabled && string.Equals(model.Id, request.ModelId, StringComparison.Ordinal));
            if (configured is not null)
            {
                EnsureAgentProviderSupported(configured.Provider);
                return string.IsNullOrWhiteSpace(request.ApiKey)
                    ? configured
                    : CloneModelWithApiKey(configured, request.ApiKey);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Endpoint) && !string.IsNullOrWhiteSpace(request.Provider))
        {
            EnsureAgentProviderSupported(request.Provider);
            return new AiModelOptions
            {
                Id = string.IsNullOrWhiteSpace(request.ModelId) ? "custom-agent" : request.ModelId,
                Name = string.IsNullOrWhiteSpace(request.ModelId) ? "自定义 Agent 模型" : request.ModelId,
                Provider = request.Provider,
                Endpoint = request.Endpoint,
                Model = "deepseek-chat",
                Enabled = true,
                IsDefault = false,
                ApiKey = request.ApiKey,
                TimeoutSeconds = _agentOptions.TimeoutSeconds
            };
        }

        if (!string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new ArgumentException($"AI 模型 '{request.ModelId}' 未配置或未启用。请在「设置 → AI 大模型配置」中添加模型。", nameof(request));
        }

        throw new InvalidOperationException("未选择 AI 模型。请在「设置 → AI 大模型配置」中选择默认模型。");
    }

    private static AiModelOptions CloneModelWithApiKey(AiModelOptions model, string apiKey)
    {
        return new AiModelOptions
        {
            Id = model.Id,
            Name = model.Name,
            Provider = model.Provider,
            Endpoint = model.Endpoint,
            Model = model.Model,
            Enabled = model.Enabled,
            IsDefault = model.IsDefault,
            ApiKey = apiKey,
            ApiKeyEnvironmentVariable = model.ApiKeyEnvironmentVariable,
            TimeoutSeconds = model.TimeoutSeconds,
            Notes = model.Notes
        };
    }

    private static void EnsureAgentProviderSupported(string provider)
    {
        if (provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("AgentBuild 当前仅支持 OpenAI-compatible Chat Completions 接口。请为 Agent 模式选择 DeepSeek 或兼容 /chat/completions 的模型。");
        }
    }

    private async Task<Dictionary<string, object?>> BuildNodeFocusContextAsync(
        NodeEntity node,
        string chatHistory,
        int maxLength,
        double usagePercent,
        string contextStatus)
    {
        var allNodes = await _nodeRepository.ListByMapAsync(node.MapId);
        var parentChain = new List<Dictionary<string, object?>>();
        var current = node.ParentId.HasValue
            ? allNodes.FirstOrDefault(n => n.Id == node.ParentId.Value)
            : null;
        while (current is not null)
        {
            parentChain.Insert(0, NodeToFocusDto(current));
            current = current.ParentId.HasValue
                ? allNodes.FirstOrDefault(n => n.Id == current.ParentId.Value)
                : null;
        }

        var children = allNodes
            .Where(n => n.ParentId == node.Id)
            .OrderBy(n => n.OrderNo)
            .ThenBy(n => n.Id)
            .Take(20)
            .Select(NodeToFocusDto)
            .ToList();

        var relations = await _relationRepository.ListByNodeAsync(node.Id);
        var relationItems = relations.Select(relation =>
        {
            var isSource = relation.SourceId == node.Id;
            var otherId = isSource ? relation.TargetId : relation.SourceId;
            var otherNode = allNodes.FirstOrDefault(n => n.Id == otherId);
            return new Dictionary<string, object?>
            {
                ["relation_id"] = relation.Id,
                ["direction"] = isSource ? "outgoing" : "incoming",
                ["relation_type"] = relation.RelationType,
                ["weight"] = relation.Weight,
                ["other_node_id"] = otherId,
                ["other_node_title"] = otherNode?.Title ?? (isSource ? relation.TargetTitle : relation.SourceTitle) ?? $"节点#{otherId}"
            };
        }).ToList();

        return new Dictionary<string, object?>
        {
            ["mode"] = "node-question-agent",
            ["domain_and_skill_binding"] = "default",
            ["current_node"] = NodeToFocusDto(node),
            ["parent_chain"] = parentChain,
            ["children"] = children,
            ["relations"] = relationItems,
            ["chat_history"] = string.IsNullOrWhiteSpace(chatHistory) ? "（无历史上下文）" : chatHistory,
            ["context_budget"] = new Dictionary<string, object?>
            {
                ["max_context_length"] = maxLength,
                ["usage_percent"] = usagePercent,
                ["status"] = contextStatus
            }
        };
    }

    private static Dictionary<string, object?> BuildAgentContext(
        JsonElement? previousContext,
        Dictionary<string, object?> focusContext)
    {
        var workingMemory = new Dictionary<string, object?>();
        if (previousContext.HasValue &&
            previousContext.Value.ValueKind != JsonValueKind.Undefined &&
            previousContext.Value.ValueKind != JsonValueKind.Null)
        {
            workingMemory["previous_context_update"] = previousContext.Value.Clone();
            if (previousContext.Value.ValueKind == JsonValueKind.Object &&
                previousContext.Value.TryGetProperty("summary", out var summary) &&
                summary.ValueKind == JsonValueKind.String)
            {
                workingMemory["previous_summary"] = summary.GetString();
            }
        }

        return new Dictionary<string, object?>
        {
            ["long_term_memory"] = new Dictionary<string, object?>(),
            ["working_memory"] = workingMemory,
            ["focus_context"] = focusContext
        };
    }

    private string ResolveAgentBuildRoot(string? requestPath)
    {
        var configuredPath = string.IsNullOrWhiteSpace(requestPath)
            ? _agentOptions.AgentBuildPath
            : requestPath.Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("请在「设置 → AgentBuild 脚本设置」中配置 AgentBuild 目录。");
        }

        configuredPath = Environment.ExpandEnvironmentVariables(configuredPath);
        string root;
        if (File.Exists(configuredPath))
        {
            var file = new FileInfo(configuredPath);
            root = file.Name.Equals("agent_kernel.py", StringComparison.OrdinalIgnoreCase) &&
                file.Directory?.Parent is not null
                    ? file.Directory.Parent.FullName
                    : file.Directory?.FullName ?? string.Empty;
        }
        else if (Directory.Exists(configuredPath))
        {
            var directory = new DirectoryInfo(configuredPath);
            root = directory.Name.Equals("src", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(directory.FullName, "agent_kernel.py")) &&
                directory.Parent is not null
                    ? directory.Parent.FullName
                    : directory.FullName;
        }
        else
        {
            throw new InvalidOperationException($"AgentBuild 路径不存在：{configuredPath}");
        }

        var kernelFile = Path.Combine(root, "src", "agent_kernel.py");
        if (!File.Exists(kernelFile))
        {
            throw new InvalidOperationException($"AgentBuild 内核脚本不存在：{kernelFile}");
        }

        return root;
    }

    private static Dictionary<string, object?> NodeToFocusDto(NodeEntity node)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = node.Id,
            ["map_id"] = node.MapId,
            ["map_title"] = node.MapTitle,
            ["parent_id"] = node.ParentId,
            ["title"] = node.Title,
            ["content"] = node.Content,
            ["order_no"] = node.OrderNo
        };
    }

    private static IReadOnlyList<JsonElement> CloneElements(IReadOnlyList<JsonElement> values)
    {
        return values.Select(value => value.Clone()).ToList();
    }

    private static JsonElement EmptyJsonObject()
    {
        return JsonSerializer.Deserialize<JsonElement>("{}");
    }

    private static string JoinLines(IReadOnlyList<string> lines, string fallback)
    {
        var text = string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
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

    private static string BuildRedactedPayloadJson(Dictionary<string, object?> kernelRequest)
    {
        var cloned = new Dictionary<string, object?>(kernelRequest);
        if (cloned.TryGetValue("model_config", out var modelConfigValue) &&
            modelConfigValue is Dictionary<string, object?> modelConfig)
        {
            var redactedModelConfig = new Dictionary<string, object?>(modelConfig);
            if (redactedModelConfig.ContainsKey("api_key"))
            {
                redactedModelConfig["api_key"] = "***";
            }
            cloned["model_config"] = redactedModelConfig;
        }

        return JsonSerializer.Serialize(cloned, JsonOptions);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup after timeout.
        }
    }

    private sealed class AgentKernelResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("agent_target")]
        public string AgentTarget { get; init; } = string.Empty;

        [JsonPropertyName("main_text")]
        public string MainText { get; init; } = string.Empty;

        [JsonPropertyName("skill_calls")]
        public IReadOnlyList<JsonElement> SkillCalls { get; init; } = Array.Empty<JsonElement>();

        [JsonPropertyName("context_update")]
        public JsonElement ContextUpdate { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
