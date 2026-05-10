import { ref, computed } from 'vue';
import { api } from '../services/api';

const STORAGE_KEY_CONTEXT = 'netmind_context_length';

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

export function useNodeAiChat(initialMode = 'node') {
  const messages = ref([]);
  const inputText = ref('');
  const loading = ref(false);
  const contextUsagePercent = ref(0);
  const contextStatus = ref('comfortable');
  const compressedContext = ref('');
  const lastResult = ref(null);

  const maxContextLength = ref(loadMaxContextLength());
  const chatMode = ref(initialMode);

  function conversationPrefix() {
    if (chatMode.value === 'app-help') return 'help';
    if (chatMode.value === 'map') return 'map';
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
      .map(m => `${m.role === 'user' ? '用户' : 'AI'}：${m.content}`)
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

  async function sendMessage(node, modelId, apiKey, mapId) {
    const text = inputText.value.trim();
    if (!text) return;

    // App-help mode and map mode don't require a node
    if (chatMode.value !== 'app-help' && chatMode.value !== 'map' && !node) return;
    // Map mode requires a mapId
    if (chatMode.value === 'map' && !mapId) return;

    inputText.value = '';
    messages.value.push({ role: 'user', content: text });
    loading.value = true;

    refreshMaxContextLength();

    try {
      let endpoint, body;

      if (chatMode.value === 'app-help') {
        endpoint = '/api/ai/app-help-chat';
        body = {
          message: text,
          context: getContextText(),
          conversationId: conversationId.value,
          modelId: modelId || null,
          apiKey: apiKey || null,
          maxContextLength: maxContextLength.value
        };
      } else if (chatMode.value === 'map') {
        endpoint = '/api/ai/map-chat';
        body = {
          mapId: Number(mapId),
          message: text,
          context: getContextText(),
          conversationId: conversationId.value,
          modelId: modelId || null,
          apiKey: apiKey || null,
          maxContextLength: maxContextLength.value
        };
      } else {
        endpoint = '/api/ai/node-chat';
        body = {
          nodeId: node.id,
          message: text,
          context: getContextText(),
          conversationId: conversationId.value,
          modelId: modelId || null,
          apiKey: apiKey || null,
          maxContextLength: maxContextLength.value
        };
      }

      const result = await api(endpoint, {
        method: 'POST',
        body: JSON.stringify(body)
      });

      if (result) {
        messages.value.push({ role: 'assistant', content: result.reply });
        contextUsagePercent.value = result.contextUsagePercent ?? 0;
        contextStatus.value = result.contextStatus ?? 'comfortable';

        if (result.wasContextCompressed && result.compressedContext) {
          compressedContext.value = result.compressedContext;
          contextUsagePercent.value = (result.compressedContext.length / maxContextLength.value) * 100;
        }

        lastResult.value = result;
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
    chatMode,
    conversationId,
    historyOpen,
    historyLoading,
    historyGroups,
    historyError,
    sendMessage,
    clearChat,
    startNewConversation,
    loadHistory,
    restoreConversation,
    refreshMaxContextLength
  };
}