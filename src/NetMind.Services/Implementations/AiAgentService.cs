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
    private const string DefaultDomain = "netmind";
    private const string AgentKernelApiVersion = "v2";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AiAgentOptions _agentOptions;
    private readonly AiCleanOptions _aiOptions;
    private readonly IMindMapRepository _mindMapRepository;
    private readonly INodeRepository _nodeRepository;
    private readonly INodeRelationRepository _relationRepository;
    private readonly IAppLogger _logger;

    public AiAgentService(
        AiAgentOptions agentOptions,
        AiCleanOptions aiOptions,
        IMindMapRepository mindMapRepository,
        INodeRepository nodeRepository,
        INodeRelationRepository relationRepository,
        IAppLogger logger)
    {
        _agentOptions = agentOptions;
        _aiOptions = aiOptions;
        _mindMapRepository = mindMapRepository;
        _nodeRepository = nodeRepository;
        _relationRepository = relationRepository;
        _logger = logger;
    }

    public async Task<AiAgentChatResult> ChatWithNodeAgentAsync(AiNodeAgentChatRequest request)
    {
        if (request.NodeId <= 0)
        {
            throw new ArgumentException("请选择有效的节点。", nameof(request));
        }

        var node = await _nodeRepository.GetAsync(request.NodeId);
        if (node is null)
        {
            throw new ArgumentException("节点不存在。", nameof(request));
        }

        return await ChatWithAgentAsync(
            request,
            _agentOptions.NodeQuestion,
            "node-agent",
            (chatHistory, maxLength, usagePercent, contextStatus) =>
                BuildNodeFocusContextAsync(node, chatHistory, maxLength, usagePercent, contextStatus));
    }

    public async Task<AiAgentChatResult> ChatWithMapAgentAsync(AiMapAgentChatRequest request)
    {
        if (request.MapId <= 0)
        {
            throw new ArgumentException("请选择有效的思维导图。", nameof(request));
        }

        var map = await _mindMapRepository.GetAsync(request.MapId);
        if (map is null)
        {
            throw new ArgumentException("思维导图不存在。", nameof(request));
        }

        return await ChatWithAgentAsync(
            request,
            _agentOptions.MapQuestion,
            "map-agent",
            (chatHistory, maxLength, usagePercent, contextStatus) =>
                BuildMapFocusContextAsync(map, chatHistory, maxLength, usagePercent, contextStatus));
    }

    public async Task<AiAgentChatResult> ChatWithGlobalAgentAsync(AiGlobalAgentChatRequest request)
    {
        return await ChatWithAgentAsync(
            request,
            _agentOptions.GlobalQuestion,
            "global-agent",
            (chatHistory, maxLength, usagePercent, contextStatus) =>
                Task.FromResult(BuildGlobalFocusContext(chatHistory, maxLength, usagePercent, contextStatus)));
    }

    public async Task<AiAgentChatResult> ChatWithAppHelpAgentAsync(AiAppHelpAgentChatRequest request)
    {
        return await ChatWithAgentAsync(
            request,
            _agentOptions.AppHelp,
            "help-agent",
            (chatHistory, maxLength, usagePercent, contextStatus) =>
                Task.FromResult(BuildAppHelpFocusContext(chatHistory, maxLength, usagePercent, contextStatus)));
    }

    private async Task<AiAgentChatResult> ChatWithAgentAsync(
        AiAgentChatRequest request,
        AiAgentScenarioOptions scenario,
        string conversationPrefix,
        Func<string, int, double, string, Task<Dictionary<string, object?>>> buildFocusContextAsync)
    {
        if (string.IsNullOrWhiteSpace(request.Message) && ResolveConfirmedToolCalls(request).Count == 0)
        {
            throw new ArgumentException("请输入对话内容。", nameof(request));
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

        var selectedModel = ResolveAgentModel(request);
        var modelConfig = BuildModelConfig(selectedModel, request.ApiKey);
        var focusContext = await buildFocusContextAsync(contextText, maxLength, usagePercent, contextStatus);
        var agentContext = BuildAgentContext(request.AgentContext, focusContext);
        var kernelRoot = ResolveAgentBuildRoot(request.AgentBuildPath);
        var kernelRequest = BuildKernelRequest(request, scenario, modelConfig, agentContext, conversationPrefix);
        var promptForLog = BuildRedactedPayloadJson(kernelRequest);
        var kernelResponse = await RunKernelAsync(kernelRoot, kernelRequest);

        return new AiAgentChatResult
        {
            SelectedModel = ToDto(selectedModel),
            Prompt = promptForLog,
            Reply = BuildAgentReply(kernelResponse),
            Status = kernelResponse.Status,
            AgentTarget = kernelResponse.AgentTarget,
            ToolCalls = CloneElements(kernelResponse.ToolCalls),
            ContextUpdate = kernelResponse.ContextUpdate.ValueKind == JsonValueKind.Undefined
                ? EmptyJsonObject()
                : kernelResponse.ContextUpdate.Clone(),
            ContextUsagePercent = usagePercent,
            ContextStatus = contextStatus,
            Warnings = Array.Empty<string>()
        };
    }

    private static string BuildAgentReply(AgentKernelResponse kernelResponse)
    {
        if (!string.IsNullOrWhiteSpace(kernelResponse.MainText))
        {
            return kernelResponse.MainText;
        }

        if (kernelResponse.Status.Equals("error", StringComparison.OrdinalIgnoreCase))
        {
            return kernelResponse.Error ?? "Agent 执行失败。";
        }

        return kernelResponse.Status.Equals("final", StringComparison.OrdinalIgnoreCase)
            ? "Agent 未返回正文。"
            : string.Empty;
    }

    private Dictionary<string, object?> BuildKernelRequest(
        AiAgentChatRequest request,
        AiAgentScenarioOptions scenario,
        Dictionary<string, object?> modelConfig,
        Dictionary<string, object?> agentContext,
        string conversationPrefix)
    {
        var domain = ResolveDomain(request.Domain, scenario.Domain);
        ApplyDomain(agentContext, domain);

        var userText = string.IsNullOrWhiteSpace(request.Message)
            ? "用户已处理上一轮 Agent Tool 权限，请继续完成任务。"
            : request.Message.Trim();

        return new Dictionary<string, object?>
        {
            ["api_version"] = AgentKernelApiVersion,
            ["conversation_id"] = string.IsNullOrWhiteSpace(request.ConversationId)
                ? $"{conversationPrefix}-{Guid.NewGuid():N}"
                : request.ConversationId,
            ["user_text"] = userText,
            ["domain"] = domain,
            ["identity"] = JoinLines(scenario.IdentityLines, "你是 NetMind 的节点问答 Agent。"),
            ["cues"] = JoinLines(scenario.CuesLines, "使用中文，围绕当前节点上下文回答。"),
            ["model_config"] = modelConfig,
            ["context"] = agentContext,
            ["tool_runtime"] = BuildToolRuntime(),
            ["confirmed_tool_calls"] = NormalizeConfirmedToolCalls(ResolveConfirmedToolCalls(request)),
            ["history_tool_calls"] = CloneElements(ResolveHistoryToolCalls(request))
        };
    }

    private Dictionary<string, object?> BuildToolRuntime()
    {
        return new Dictionary<string, object?>
        {
            ["shared"] = new Dictionary<string, object?>
            {
                ["netmind_api_base_url"] = NormalizeBaseUrl(_agentOptions.NetMindApiBaseUrl),
                ["timeout_seconds"] = Math.Max(_agentOptions.ToolRuntimeTimeoutSeconds, 1)
            },
            ["tools"] = new Dictionary<string, object?>()
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
                $"AI 模型 '{model.Name}' 缺少 API Key。请在「设置 → AI 大模型配置」中为模型配置 API Key。");
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
                Model = string.IsNullOrWhiteSpace(request.Model) ? "deepseek-chat" : request.Model.Trim(),
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

    private async Task<Dictionary<string, object?>> BuildMapFocusContextAsync(
        MindMapEntity map,
        string chatHistory,
        int maxLength,
        double usagePercent,
        string contextStatus)
    {
        var nodes = await _nodeRepository.ListByMapAsync(map.Id);
        var relations = await _relationRepository.ListByMapAsync(map.Id);
        var nodeById = nodes.ToDictionary(node => node.Id);
        var nodeItems = nodes
            .OrderBy(node => node.ParentId.HasValue ? 1 : 0)
            .ThenBy(node => node.ParentId ?? 0)
            .ThenBy(node => node.OrderNo)
            .ThenBy(node => node.Id)
            .Select(NodeToFocusDto)
            .ToList();
        var relationItems = relations
            .OrderBy(relation => relation.SourceId)
            .ThenBy(relation => relation.TargetId)
            .ThenBy(relation => relation.Id)
            .Select(relation => RelationToFocusDto(relation, nodeById))
            .ToList();

        return new Dictionary<string, object?>
        {
            ["mode"] = "map-question-agent",
            ["map"] = new Dictionary<string, object?>
            {
                ["id"] = map.Id,
                ["title"] = map.Title,
                ["root_node_id"] = map.RootNodeId,
                ["created_at"] = map.CreatedAt,
                ["updated_at"] = map.UpdatedAt
            },
            ["nodes"] = nodeItems,
            ["relations"] = relationItems,
            ["statistics"] = new Dictionary<string, object?>
            {
                ["node_count"] = nodeItems.Count,
                ["relation_count"] = relationItems.Count
            },
            ["chat_history"] = string.IsNullOrWhiteSpace(chatHistory) ? "（无历史上下文）" : chatHistory,
            ["context_budget"] = new Dictionary<string, object?>
            {
                ["max_context_length"] = maxLength,
                ["usage_percent"] = usagePercent,
                ["status"] = contextStatus
            }
        };
    }

    private static Dictionary<string, object?> BuildGlobalFocusContext(
        string chatHistory,
        int maxLength,
        double usagePercent,
        string contextStatus)
    {
        return new Dictionary<string, object?>
        {
            ["mode"] = "global-question-agent",
            ["base_info"] = new Dictionary<string, object?>
            {
                ["product_name"] = "NetMind",
                ["product_scope"] = "基于 AI 的知识网络构建与可视化工具。",
                ["agent_scope"] = "全局问答 Agent 仅接收用户问题、对话历史、Agent 记忆和基础应用信息。",
                ["data_scope"] = "本模式不传递任何节点、关联关系或思维导图数据。"
            },
            ["chat_history"] = string.IsNullOrWhiteSpace(chatHistory) ? "（无历史上下文）" : chatHistory,
            ["context_budget"] = new Dictionary<string, object?>
            {
                ["max_context_length"] = maxLength,
                ["usage_percent"] = usagePercent,
                ["status"] = contextStatus
            }
        };
    }

    private Dictionary<string, object?> BuildAppHelpFocusContext(
        string chatHistory,
        int maxLength,
        double usagePercent,
        string contextStatus)
    {
        var manualPath = string.IsNullOrWhiteSpace(_aiOptions.Prompt.AppManualPath)
            ? "应用帮助说明书路径未配置。"
            : _aiOptions.Prompt.AppManualPath;
        var learningPath = string.IsNullOrWhiteSpace(_aiOptions.Prompt.AppHelpLearningPath)
            ? "应用帮助学习记录路径未配置。"
            : _aiOptions.Prompt.AppHelpLearningPath;
        var usageTipsPath = string.IsNullOrWhiteSpace(_aiOptions.Prompt.AppHelpUsageTipsPath)
            ? "应用帮助使用技巧路径未配置。"
            : _aiOptions.Prompt.AppHelpUsageTipsPath;

        return new Dictionary<string, object?>
        {
            ["mode"] = "app-help-agent",
            ["base_info"] = new Dictionary<string, object?>
            {
                ["product_name"] = "NetMind",
                ["product_scope"] = "基于 AI 的知识网络构建与可视化工具。",
                ["agent_scope"] = "应用帮助 Agent 负责解释软件功能、操作路径、部署配置和常见问题。",
                ["data_scope"] = "本模式不默认传递节点、关联关系或思维导图业务数据。"
            },
            ["manual_absolute_path"] = manualPath,
            ["manual_access_policy"] = "说明书是管理员维护的正式文档；Agent 只能读取说明书，不允许直接修改说明书原文。",
            ["learning_log_absolute_path"] = learningPath,
            ["learning_log_update_policy"] = "对话中学到稳定的软件操作、限制、排障步骤或说明缺口时，只能向学习记录追加增量内容；不允许删除、覆盖或重写已有学习经验。管理员后续统一筛选并维护正式说明书。",
            ["usage_tips_absolute_path"] = usageTipsPath,
            ["usage_tips_update_policy"] = "确认技巧稳定且可复用后，Agent 可使用 incremental_file_modifier 对使用技巧文档做小范围增量维护；允许补充、修正和合并技巧，但不能改写正式说明书。",
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

    private static string ResolveDomain(string? requestDomain, string? scenarioDomain)
    {
        var domain = string.IsNullOrWhiteSpace(requestDomain)
            ? scenarioDomain
            : requestDomain.Trim();

        return string.IsNullOrWhiteSpace(domain) ? DefaultDomain : domain.Trim();
    }

    private static void ApplyDomain(Dictionary<string, object?> agentContext, string domain)
    {
        if (agentContext.TryGetValue("focus_context", out var focusContextValue) &&
            focusContextValue is Dictionary<string, object?> focusContext)
        {
            focusContext["domain"] = domain;
        }
    }

    private static IReadOnlyList<JsonElement> ResolveConfirmedToolCalls(AiAgentChatRequest request)
    {
        return request.ConfirmedToolCalls ?? Array.Empty<JsonElement>();
    }

    private static IReadOnlyList<JsonElement> ResolveHistoryToolCalls(AiAgentChatRequest request)
    {
        return request.HistoryToolCalls ?? Array.Empty<JsonElement>();
    }

    private static IReadOnlyList<object?> NormalizeConfirmedToolCalls(IReadOnlyList<JsonElement>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<object?>();
        }

        return values.Select(NormalizeConfirmedToolCall).ToList();
    }

    private static object? NormalizeConfirmedToolCall(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return value.Clone();
        }

        var item = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            item[property.Name] = property.Value.Clone();
        }

        if (IsApprovedFalse(value) && !HasStringProperty(value, "reject_reason"))
        {
            var rejectReason = ReadFirstStringProperty(value, "denied_reason", "deny_reason", "reason");
            if (!string.IsNullOrWhiteSpace(rejectReason))
            {
                item["reject_reason"] = rejectReason;
            }
        }

        return item;
    }

    private static bool IsApprovedFalse(JsonElement value)
    {
        return value.TryGetProperty("approved", out var approved) &&
            approved.ValueKind == JsonValueKind.False;
    }

    private static bool HasStringProperty(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString());
    }

    private static string? ReadFirstStringProperty(JsonElement value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (value.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                return property.GetString();
            }
        }

        return null;
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

    private static Dictionary<string, object?> RelationToFocusDto(
        NodeRelationEntity relation,
        IReadOnlyDictionary<long, NodeEntity> nodeById)
    {
        nodeById.TryGetValue(relation.SourceId, out var sourceNode);
        nodeById.TryGetValue(relation.TargetId, out var targetNode);
        return new Dictionary<string, object?>
        {
            ["id"] = relation.Id,
            ["map_id"] = relation.MapId,
            ["source_id"] = relation.SourceId,
            ["source_title"] = sourceNode?.Title ?? relation.SourceTitle ?? $"节点#{relation.SourceId}",
            ["target_id"] = relation.TargetId,
            ["target_title"] = targetNode?.Title ?? relation.TargetTitle ?? $"节点#{relation.TargetId}",
            ["relation_type"] = relation.RelationType,
            ["weight"] = relation.Weight
        };
    }

    private static IReadOnlyList<JsonElement> CloneElements(IReadOnlyList<JsonElement>? values)
    {
        return values is null
            ? Array.Empty<JsonElement>()
            : values.Select(value => value.Clone()).ToList();
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
        return null;
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://127.0.0.1:5120"
            : baseUrl.Trim();
        return value.TrimEnd('/');
    }

    private static AiModelOptionDto ToDto(AiModelOptions model)
    {
        return new AiModelOptionDto
        {
            Id = model.Id,
            Name = model.Name,
            Provider = model.Provider,
            Endpoint = model.Endpoint,
            Model = model.Model,
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

        [JsonPropertyName("tool_calls")]
        public IReadOnlyList<JsonElement> ToolCalls { get; init; } = Array.Empty<JsonElement>();

        [JsonPropertyName("context_update")]
        public JsonElement ContextUpdate { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
