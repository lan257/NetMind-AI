import { ref, computed } from 'vue';
import { api } from '../services/api';
import { getGlobalModelConfig } from './useGlobalModel';

const STORAGE_KEY_CONTEXT = 'netmind_context_length';
const STORAGE_KEY_AGENTBUILD_PATH = 'netmind_agentbuild_path';
const DEFAULT_AGENTBUILD_PATH = 'G:\\AAW+\\NetMind\\AgentBuild';

function loadMaxContextLength() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_CONTEXT);
    return raw ? parseInt(raw, 10) : 51200;
  } catch {
    return 51200;
  }
}

function createConversationId(prefix) {
  const uuid = window.crypto?.randomUUID
    ? window.crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `${prefix}-${uuid}`;
}

function loadAgentBuildPath() {
  try {
    return localStorage.getItem(STORAGE_KEY_AGENTBUILD_PATH) || DEFAULT_AGENTBUILD_PATH;
  } catch {
    return DEFAULT_AGENTBUILD_PATH;
  }
}

function isAgentMode(mode) {
  return mode === 'node-agent' || mode === 'map-agent' || mode === 'global' || mode === 'app-help';
}

function requiresNode(mode) {
  return mode === 'node-agent';
}

function requiresMap(mode) {
  return mode === 'map-agent';
}

function getAgentEndpoint(mode) {
  if (mode === 'map-agent') return '/api/ai/map-agent-chat';
  if (mode === 'global') return '/api/ai/global-agent-chat';
  if (mode === 'app-help') return '/api/ai/app-help-agent-chat';
  return '/api/ai/node-agent-chat';
}

function getCallId(call) {
  return call?.call_id || call?.callId || '';
}

function getToolId(call) {
  return call?.tool_id || call?.toolId || call?.skill_id || call?.skillId || '';
}

function getToolName(call) {
  return call?.tool_name ||
    call?.toolName ||
    call?.tool_id ||
    call?.toolId ||
    call?.skill_name ||
    call?.skillName ||
    call?.skill_id ||
    call?.skillId ||
    '工具';
}

function getToolStatus(call) {
  return call?.execution?.status || '';
}

function buildDeniedMessage(rejectReason) {
  const reason = rejectReason?.trim();
  return reason ? `用户拒绝授权：${reason}` : '用户拒绝授权';
}

function isAgentProgressStatus(status) {
  const normalized = `${status || ''}`.toLowerCase();
  return Boolean(normalized) && normalized !== 'final' && normalized !== 'error';
}

function buildAgentProgressText(result) {
  const text = result.reply || result.mainText || '';
  if (text.trim()) return text;
  if (result.agentTarget) return `正在处理：${result.agentTarget}`;
  return 'Agent 正在推进任务。';
}

function getWaitingToolCalls(toolCalls) {
  return (toolCalls || []).filter((call) => {
    return getCallId(call) && getToolStatus(call) === 'waiting_permission';
  });
}

function buildToolContinuationText(decisions) {
  const hasDeniedTool = decisions.some((decision) => decision.approved === false);
  return hasDeniedTool
    ? '用户已处理上一轮 Agent 工具权限，其中存在拒绝项。请根据权限结果继续完成任务。'
    : '用户已处理上一轮 Agent 工具权限，请继续完成任务。';
}

export function useNodeAiChat(initialMode = 'node-agent') {
  const messages = ref([]);
  const inputText = ref('');
  const loading = ref(false);
  const contextUsagePercent = ref(0);
  const contextStatus = ref('comfortable');
  const compressedContext = ref('');
  const agentContext = ref(null);
  const historyToolCalls = ref([]);
  const pendingToolDecisions = ref([]);
  const lastResult = ref(null);

  const maxContextLength = ref(loadMaxContextLength());
  const chatMode = ref(initialMode);

  function conversationPrefix() {
    if (chatMode.value === 'app-help') return 'help';
    if (chatMode.value === 'map-agent') return 'map-agent';
    if (chatMode.value === 'node-agent') return 'node-agent';
    if (chatMode.value === 'global') return 'global';
    return 'node-agent';
  }
  const conversationId = ref(createConversationId(conversationPrefix()));

  // History state
  const historyOpen = ref(false);
  const historyLoading = ref(false);
  const historyGroups = ref([]);
  const historyError = ref('');

  const contextText = computed(() => {
    return messages.value
      .map(m => `${m.role === 'user' ? '用户' : m.role === 'assistant' ? 'AI' : '系统'}：${m.content}`)
      .join('\n\n');
  });

  const contextUsageLabel = computed(() => {
    const pct = contextUsagePercent.value;
    if (pct > 100) return '超限';
    if (pct > 80) return '紧张';
    if (pct > 60) return '压缩中';
    return '宽裕';
  });

  const contextUsageClass = computed(() => {
    const pct = contextUsagePercent.value;
    if (pct > 100) return 'critical';
    if (pct > 80) return 'warning';
    if (pct > 60) return 'caution';
    return 'good';
  });

  function getContextText() {
    return compressedContext.value || contextText.value;
  }

  function refreshMaxContextLength() {
    maxContextLength.value = loadMaxContextLength();
  }

  function applyChatResult(result) {
    const isAgentProgress = isAgentMode(chatMode.value) && isAgentProgressStatus(result.status);
    const toolCalls = result.toolCalls || result.skillCalls || [];
    const assistantMessage = {
      role: 'assistant',
      content: isAgentProgress ? '' : (result.reply || result.mainText || '')
    };

    if (toolCalls.length || result.status || result.agentTarget) {
      assistantMessage.agent = {
        status: result.status,
        agentTarget: result.agentTarget,
        progressText: isAgentProgress ? buildAgentProgressText(result) : '',
        toolCalls
      };
    }

    messages.value.push(assistantMessage);
    contextUsagePercent.value = result.contextUsagePercent ?? 0;
    contextStatus.value = result.contextStatus ?? 'comfortable';

    if (result.wasContextCompressed && result.compressedContext) {
      compressedContext.value = result.compressedContext;
      contextUsagePercent.value = (result.compressedContext.length / maxContextLength.value) * 100;
    }

    if (isAgentMode(chatMode.value)) {
      agentContext.value = result.contextUpdate || null;
      historyToolCalls.value = toolCalls;
      pendingToolDecisions.value = [];
    }

    lastResult.value = result;
  }

  function markToolCallDecision(call, approved, rejectReason = '') {
    const callId = getCallId(call);
    if (!callId) return;
    const deniedMessage = buildDeniedMessage(rejectReason);

    messages.value = messages.value.map((message) => {
      if (!message.agent?.toolCalls?.length) return message;

      return {
        ...message,
        agent: {
          ...message.agent,
          toolCalls: message.agent.toolCalls.map((toolCall) => {
            if (getCallId(toolCall) !== callId) return toolCall;

            return {
              ...toolCall,
              permission: {
                ...(toolCall.permission || {}),
                approved,
                ...(approved || !rejectReason?.trim() ? {} : { reject_reason: rejectReason.trim() })
              },
              execution: {
                ...(toolCall.execution || {}),
                status: approved ? 'permission_approved' : 'permission_denied',
                success: approved ? null : false,
                ...(approved || !rejectReason?.trim() ? {} : { denied_reason: rejectReason.trim() }),
                error: approved ? toolCall.execution?.error ?? null : deniedMessage
              }
            };
          })
        }
      };
    });
  }

  function saveToolDecision(decision) {
    const nextDecisions = pendingToolDecisions.value.filter((item) => item.call_id !== decision.call_id);
    pendingToolDecisions.value = [...nextDecisions, decision];
  }

  function hasUndecidedWaitingToolCalls() {
    const decidedCallIds = new Set(pendingToolDecisions.value.map((decision) => decision.call_id));
    return getWaitingToolCalls(historyToolCalls.value).some((call) => !decidedCallIds.has(getCallId(call)));
  }

  function buildAgentRequestBody({ node, mapId, message, modelConfig, confirmedToolCalls = [] }) {
    const body = {
      message,
      context: getContextText(),
      conversationId: conversationId.value,
      modelId: modelConfig.modelId || null,
      endpoint: modelConfig.endpoint || null,
      provider: modelConfig.provider || null,
      apiKey: modelConfig.apiKey || null,
      maxContextLength: maxContextLength.value,
      agentBuildPath: loadAgentBuildPath(),
      agentContext: agentContext.value,
      historyToolCalls: historyToolCalls.value,
      confirmedToolCalls
    };

    if (chatMode.value === 'node-agent') {
      body.nodeId = node.id;
    } else if (chatMode.value === 'map-agent') {
      body.mapId = Number(mapId);
    }

    return body;
  }

  /**
   * 发送消息。模型配置从全局设置中自动读取。
   * @param {Object|null} node - 当前节点（非节点模式可为 null）
   * @param {number|null} mapId - 当前导图 ID（全图模式必须）
   */
  async function sendMessage(node, mapId) {
    const text = inputText.value.trim();
    if (!text) return;

    if (requiresNode(chatMode.value) && !node) return;
    if (requiresMap(chatMode.value) && !mapId) return;

    inputText.value = '';
    messages.value.push({ role: 'user', content: text });
    loading.value = true;

    refreshMaxContextLength();

    // 从全局设置读取模型配置
    const modelConfig = getGlobalModelConfig();

    try {
      let endpoint, body;

      if (isAgentMode(chatMode.value)) {
        endpoint = getAgentEndpoint(chatMode.value);
        body = buildAgentRequestBody({
          node,
          mapId,
          message: text,
          modelConfig,
          confirmedToolCalls: []
        });
      } else {
        endpoint = '/api/ai/node-chat';
        body = {
          nodeId: node.id,
          message: text,
          context: getContextText(),
          conversationId: conversationId.value,
          modelId: modelConfig.modelId || null,
          endpoint: modelConfig.endpoint || null,
          provider: modelConfig.provider || null,
          apiKey: modelConfig.apiKey || null,
          maxContextLength: maxContextLength.value
        };
      }

      const result = await api(endpoint, {
        method: 'POST',
        body: JSON.stringify(body)
      });

      if (result) {
        applyChatResult(result);
      }
    } catch (err) {
      messages.value.push({
        role: 'system',
        content: '请求失败：' + (err.message || '未知错误')
      });
    } finally {
      loading.value = false;
    }
  }

  async function confirmToolCall(call, approved, node, mapId, rejectReason = '') {
    if (!isAgentMode(chatMode.value) || loading.value) return;
    if (requiresNode(chatMode.value) && !node) return;
    if (requiresMap(chatMode.value) && !mapId) return;
    if (getToolStatus(call) !== 'waiting_permission') return;

    markToolCallDecision(call, approved, rejectReason);

    const toolName = getToolName(call);
    const reasonText = rejectReason?.trim();
    messages.value.push({
      role: 'system',
      content: approved
        ? `已允许执行：${toolName}`
        : `已拒绝执行：${toolName}${reasonText ? `\n原因：${reasonText}` : ''}`
    });

    const confirmedToolCall = {
      call_id: getCallId(call),
      ...(getToolId(call) ? { tool_id: getToolId(call) } : {}),
      approved
    };
    if (!approved && reasonText) {
      confirmedToolCall.reject_reason = reasonText;
    }
    saveToolDecision(confirmedToolCall);

    if (hasUndecidedWaitingToolCalls()) {
      return;
    }

    loading.value = true;
    refreshMaxContextLength();
    const modelConfig = getGlobalModelConfig();
    try {
      const result = await api(getAgentEndpoint(chatMode.value), {
        method: 'POST',
        body: JSON.stringify(buildAgentRequestBody({
          node,
          mapId,
          message: buildToolContinuationText(pendingToolDecisions.value),
          modelConfig,
          confirmedToolCalls: pendingToolDecisions.value
        }))
      });

      if (result) {
        applyChatResult(result);
      }
    } catch (err) {
      messages.value.push({
        role: 'system',
        content: '请求失败：' + (err.message || '未知错误')
      });
    } finally {
      loading.value = false;
    }
  }

  function clearChat() {
    messages.value = [];
    compressedContext.value = '';
    agentContext.value = null;
    historyToolCalls.value = [];
    pendingToolDecisions.value = [];
    contextUsagePercent.value = 0;
    contextStatus.value = 'comfortable';
    lastResult.value = null;
    conversationId.value = createConversationId(conversationPrefix());
  }

  function startNewConversation() {
    clearChat();
  }

  async function loadHistory() {
    historyOpen.value = true;
    historyLoading.value = true;
    historyError.value = '';
    try {
      const records = await api('/api/ai-conversation-records');
      const prefix = conversationPrefix();
      console.log(`[history] 总记录数: ${records.length}, 当前模式: ${chatMode.value}, 前缀: ${prefix}-`);

      // Filter by conversationId prefix for current mode
      const filtered = records.filter(r =>
        r.conversationId && r.conversationId.startsWith(prefix + '-')
      );
      console.log(`[history] 过滤后记录数: ${filtered.length}`);

      const groups = new Map();
      filtered.forEach((record) => {
        if (!groups.has(record.conversationId)) {
          groups.set(record.conversationId, []);
        }
        groups.get(record.conversationId).push(record);
      });

      historyGroups.value = [...groups.entries()]
        .map(([cid, items]) => {
          const ordered = [...items].sort(
            (a, b) => new Date(a.createdAt) - new Date(b.createdAt)
          );
          const firstUser = ordered.find((item) => item.role === 'user');
          const last = ordered[ordered.length - 1];
          return {
            conversationId: cid,
            records: ordered,
            title: firstUser?.content?.slice(0, 36) || '未命名对话',
            updatedAt: last?.updatedAt ?? last?.createdAt ?? '',
            count: ordered.length
          };
        })
        .sort((a, b) => new Date(b.updatedAt) - new Date(a.updatedAt));

      if (historyGroups.value.length === 0 && records.length === 0) {
        historyError.value = '暂无历史对话记录。请确认数据库已启动且已发送过对话消息。';
      }
    } catch (err) {
      console.error('Failed to load conversation history:', err);
      historyError.value = '加载失败：' + (err.message || '未知错误，请确认后端服务和数据库是否正常运行');
    } finally {
      historyLoading.value = false;
    }
  }

  function restoreConversation(group) {
    conversationId.value = group.conversationId;
    messages.value = group.records.map((record) => ({
      role: record.role,
      content: record.content
    }));
    compressedContext.value = '';
    agentContext.value = null;
    historyToolCalls.value = [];
    pendingToolDecisions.value = [];
    contextUsagePercent.value = 0;
    contextStatus.value = 'comfortable';
    lastResult.value = null;
    historyOpen.value = false;
  }

  return {
    messages,
    inputText,
    loading,
    contextUsagePercent,
    contextStatus,
    contextText,
    contextUsageLabel,
    contextUsageClass,
    maxContextLength,
    lastResult,
    agentContext,
    historyToolCalls,
    chatMode,
    conversationId,
    historyOpen,
    historyLoading,
    historyGroups,
    historyError,
    sendMessage,
    confirmToolCall,
    clearChat,
    startNewConversation,
    loadHistory,
    restoreConversation,
    refreshMaxContextLength
  };
}
