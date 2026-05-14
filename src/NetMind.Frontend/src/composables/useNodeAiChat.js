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
  return mode === 'node-agent' || mode === 'map-agent' || mode === 'global';
}

function requiresNode(mode) {
  return mode === 'node' || mode === 'node-agent';
}

function requiresMap(mode) {
  return mode === 'map' || mode === 'map-agent';
}

function getAgentEndpoint(mode) {
  if (mode === 'map-agent') return '/api/ai/map-agent-chat';
  if (mode === 'global') return '/api/ai/global-agent-chat';
  return '/api/ai/node-agent-chat';
}

function getCallId(call) {
  return call?.call_id || call?.callId || '';
}

function getSkillName(call) {
  return call?.skill_name || call?.skillName || call?.skill_id || call?.skillId || 'Skill';
}

function getSkillStatus(call) {
  return call?.execution?.status || '';
}

export function useNodeAiChat(initialMode = 'node') {
  const messages = ref([]);
  const inputText = ref('');
  const loading = ref(false);
  const contextUsagePercent = ref(0);
  const contextStatus = ref('comfortable');
  const compressedContext = ref('');
  const agentContext = ref(null);
  const historySkillCalls = ref([]);
  const lastResult = ref(null);

  const maxContextLength = ref(loadMaxContextLength());
  const chatMode = ref(initialMode);

  function conversationPrefix() {
    if (chatMode.value === 'app-help') return 'help';
    if (chatMode.value === 'map') return 'map';
    if (chatMode.value === 'map-agent') return 'map-agent';
    if (chatMode.value === 'node-agent') return 'node-agent';
    if (chatMode.value === 'global') return 'global';
    return 'node';
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
    const assistantMessage = {
      role: 'assistant',
      content: result.reply || result.mainText || ''
    };

    if (result.skillCalls || result.status || result.agentTarget) {
      assistantMessage.agent = {
        status: result.status,
        agentTarget: result.agentTarget,
        skillCalls: result.skillCalls || []
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
      historySkillCalls.value = result.skillCalls || [];
    }

    lastResult.value = result;
  }

  function markSkillCallDecision(call, approved) {
    const callId = getCallId(call);
    if (!callId) return;

    messages.value = messages.value.map((message) => {
      if (!message.agent?.skillCalls?.length) return message;

      return {
        ...message,
        agent: {
          ...message.agent,
          skillCalls: message.agent.skillCalls.map((skillCall) => {
            if (getCallId(skillCall) !== callId) return skillCall;

            return {
              ...skillCall,
              permission: {
                ...(skillCall.permission || {}),
                approved
              },
              execution: {
                ...(skillCall.execution || {}),
                status: approved ? 'permission_approved' : 'permission_denied',
                success: approved ? null : false,
                error: approved ? skillCall.execution?.error ?? null : '用户拒绝授权'
              }
            };
          })
        }
      };
    });
  }

  function buildAgentRequestBody({ node, mapId, message, modelConfig, confirmedSkillCalls = [] }) {
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
      domainAndSkillBinding: 'default',
      agentContext: agentContext.value,
      historySkillCalls: historySkillCalls.value,
      confirmedSkillCalls
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
          confirmedSkillCalls: []
        });
      } else if (chatMode.value === 'app-help') {
        endpoint = '/api/ai/app-help-chat';
        body = {
          message: text,
          context: getContextText(),
          conversationId: conversationId.value,
          modelId: modelConfig.modelId || null,
          endpoint: modelConfig.endpoint || null,
          provider: modelConfig.provider || null,
          apiKey: modelConfig.apiKey || null,
          maxContextLength: maxContextLength.value
        };
      } else if (chatMode.value === 'map') {
        endpoint = '/api/ai/map-chat';
        body = {
          mapId: Number(mapId),
          message: text,
          context: getContextText(),
          conversationId: conversationId.value,
          modelId: modelConfig.modelId || null,
          endpoint: modelConfig.endpoint || null,
          provider: modelConfig.provider || null,
          apiKey: modelConfig.apiKey || null,
          maxContextLength: maxContextLength.value
        };
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

  async function confirmSkillCall(call, approved, node, mapId) {
    if (!isAgentMode(chatMode.value) || loading.value) return;
    if (requiresNode(chatMode.value) && !node) return;
    if (requiresMap(chatMode.value) && !mapId) return;
    if (getSkillStatus(call) !== 'waiting_permission') return;

    loading.value = true;
    markSkillCallDecision(call, approved);
    refreshMaxContextLength();

    const skillName = getSkillName(call);
    messages.value.push({
      role: 'system',
      content: approved ? `已允许执行：${skillName}` : `已拒绝执行：${skillName}`
    });

    const modelConfig = getGlobalModelConfig();
    try {
      const result = await api(getAgentEndpoint(chatMode.value), {
        method: 'POST',
        body: JSON.stringify(buildAgentRequestBody({
          node,
          mapId,
          message: approved
            ? '用户已允许执行上一轮 Agent Skill，请继续完成任务。'
            : '用户拒绝执行上一轮 Agent Skill，请在不执行该 Skill 的前提下继续回复。',
          modelConfig,
          confirmedSkillCalls: [{ call_id: getCallId(call), approved }]
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
    historySkillCalls.value = [];
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
    historySkillCalls.value = [];
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
    historySkillCalls,
    chatMode,
    conversationId,
    historyOpen,
    historyLoading,
    historyGroups,
    historyError,
    sendMessage,
    confirmSkillCall,
    clearChat,
    startNewConversation,
    loadHistory,
    restoreConversation,
    refreshMaxContextLength
  };
}
