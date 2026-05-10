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

export function useNodeAiChat() {
  const messages = ref([]);
  const inputText = ref('');
  const loading = ref(false);
  const contextUsagePercent = ref(0);
  const contextStatus = ref('comfortable');
  const compressedContext = ref('');
  const lastResult = ref(null);

  const maxContextLength = ref(loadMaxContextLength());

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

  async function sendMessage(node, modelId, apiKey) {
    const text = inputText.value.trim();
    if (!text || !node) return;

    inputText.value = '';
    messages.value.push({ role: 'user', content: text });
    loading.value = true;

    refreshMaxContextLength();

    try {
      const result = await api('/api/ai/node-chat', {
        method: 'POST',
        body: JSON.stringify({
          nodeId: node.id,
          message: text,
          context: getContextText(),
          modelId: modelId || null,
          apiKey: apiKey || null,
          maxContextLength: maxContextLength.value
        })
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
    sendMessage,
    clearChat,
    refreshMaxContextLength
  };
}