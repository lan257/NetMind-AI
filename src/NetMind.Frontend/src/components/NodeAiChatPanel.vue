<script setup>
import { ref, watch, nextTick } from 'vue';
import { ChatDotRound, Close, ArrowRight, ArrowLeft } from '@element-plus/icons-vue';
import { useNodeAiChat } from '../composables/useNodeAiChat';

const props = defineProps({
  node: { type: Object, default: null },
  aiModels: { type: Array, default: () => [] },
  selectedModelId: { type: String, default: '' }
});

const chat = useNodeAiChat();

const collapsed = ref(true);
const chatMode = ref('node'); // current: only 'node'

const chatContainer = ref(null);

function togglePanel() {
  collapsed.value = !collapsed.value;
  if (!collapsed.value) {
    nextTick(() => {
      scrollToBottom();
    });
  }
}

function scrollToBottom() {
  if (chatContainer.value) {
    const el = chatContainer.value;
    el.scrollTop = el.scrollHeight;
  }
}

async function handleSend() {
  if (!chat.inputText.value.trim() || chat.loading.value) return;

  // Get API key from localStorage custom models or from selected model
  const modelId = props.selectedModelId;
  let apiKey = null;

  // Try to find API key from custom models in localStorage
  try {
    const raw = localStorage.getItem('netmind_custom_models');
    if (raw) {
      const customModels = JSON.parse(raw);
      const matched = customModels.find(m => m.id === modelId);
      if (matched && matched.apiKey) {
        apiKey = matched.apiKey;
      }
    }
  } catch { /* ignore */ }

  await chat.sendMessage(props.node, modelId, apiKey);
  nextTick(() => scrollToBottom());
}

function handleKeyup(event) {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault();
    handleSend();
  }
}

// Clear chat when node changes
watch(() => props.node?.id, () => {
  chat.clearChat();
});
</script>

<template>
  <div class="node-ai-chat-wrapper" :class="{ collapsed }">
    <!-- Collapsed state: expand button -->
    <div v-if="collapsed" class="chat-toggle-btn" @click="togglePanel" title="打开AI节点对话">
      <el-icon :size="18"><ChatDotRound /></el-icon>
      <span class="toggle-label">AI</span>
    </div>

    <!-- Expanded panel -->
    <div v-else class="node-ai-chat-panel">
      <div class="chat-panel-header">
        <div class="chat-mode-tabs">
          <span class="chat-mode-tab active">
            <el-icon :size="14"><ChatDotRound /></el-icon>
            节点聚焦
          </span>
        </div>
        <el-button :icon="ArrowRight" size="small" text @click="togglePanel" title="折叠面板" />
      </div>

      <!-- Context status bar -->
      <div class="context-bar" v-if="chat.messages.value.length > 0">
        <div class="context-bar-inner">
          <div class="context-bar-fill" :class="chat.contextUsageClass.value" :style="{ width: Math.min(chat.contextUsagePercent.value, 100) + '%' }"></div>
        </div>
        <div class="context-bar-text">
          <span>上下文</span>
          <span :class="chat.contextUsageClass.value">{{ chat.contextUsageLabel.value }} {{ Math.round(chat.contextUsagePercent.value) }}%</span>
          <span class="context-max">/ {{ (chat.maxContextLength.value / 1024).toFixed(0) }}K</span>
        </div>
      </div>

      <!-- Messages -->
      <div class="chat-messages" ref="chatContainer">
        <div v-if="chat.messages.value.length === 0" class="chat-empty">
          <p>AI 节点聚焦助手</p>
          <p class="chat-empty-hint">针对当前选中节点进行问答或内容完善</p>
          <p class="chat-empty-hint" v-if="!node">请先在画布或列表中选择一个节点</p>
        </div>
        <div v-for="(msg, idx) in chat.messages.value" :key="idx" :class="['chat-message', `msg-${msg.role}`]">
          <div class="msg-role">{{ msg.role === 'user' ? '你' : msg.role === 'assistant' ? 'AI' : '系统' }}</div>
          <div class="msg-content" v-text="msg.content"></div>
        </div>
        <div v-if="chat.loading.value" class="chat-message msg-assistant">
          <div class="msg-role">AI</div>
          <div class="msg-content loading-dots">思考中<span>...</span></div>
        </div>
      </div>

      <!-- Input -->
      <div class="chat-input-area">
        <el-input
          v-model="chat.inputText.value"
          type="textarea"
          :rows="2"
          placeholder="输入问题或需求…"
          :disabled="!node || chat.loading.value"
          @keyup="handleKeyup"
        />
        <el-button
          type="primary"
          :icon="ChatDotRound"
          :disabled="!chat.inputText.value.trim() || chat.loading.value || !node"
          :loading="chat.loading.value"
          size="small"
          @click="handleSend"
        >
          发送
        </el-button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.node-ai-chat-wrapper {
  position: absolute;
  top: 0;
  right: calc(100% + 8px);
  z-index: 10;
  display: flex;
  flex-direction: column;
}

.node-ai-chat-wrapper.collapsed {
  right: calc(100% + 8px);
}

.chat-toggle-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 60px;
  background: #fff;
  border: 1px solid #d8e0e8;
  border-radius: 6px;
  cursor: pointer;
  color: var(--el-color-primary);
  transition: all 0.2s;
  user-select: none;
  box-shadow: 0 1px 4px rgba(0,0,0,0.06);
}
.chat-toggle-btn:hover {
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary);
}
.toggle-label {
  font-size: 10px;
  font-weight: 600;
  margin-top: 2px;
}

.node-ai-chat-panel {
  width: 320px;
  max-height: calc(100vh - 156px);
  display: flex;
  flex-direction: column;
  background: #fff;
  border: 1px solid #d8e0e8;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0,0,0,0.08);
  overflow: hidden;
}

.chat-panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 10px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--el-fill-color-light);
  flex-shrink: 0;
}

.chat-mode-tabs {
  display: flex;
  gap: 4px;
}
.chat-mode-tab {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  font-weight: 600;
  padding: 3px 8px;
  border-radius: 4px;
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

/* Context bar */
.context-bar {
  padding: 6px 10px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  flex-shrink: 0;
}
.context-bar-inner {
  height: 4px;
  background: var(--el-fill-color);
  border-radius: 2px;
  overflow: hidden;
  margin-bottom: 3px;
}
.context-bar-fill {
  height: 100%;
  border-radius: 2px;
  transition: width 0.3s ease;
}
.context-bar-fill.good { background: var(--el-color-success); }
.context-bar-fill.caution { background: var(--el-color-warning); }
.context-bar-fill.warning { background: var(--el-color-danger); }
.context-bar-fill.critical { background: var(--el-color-danger); }

.context-bar-text {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.context-bar-text .good { color: var(--el-color-success); font-weight: 600; }
.context-bar-text .caution { color: var(--el-color-warning); font-weight: 600; }
.context-bar-text .warning { color: var(--el-color-danger); font-weight: 600; }
.context-bar-text .critical { color: var(--el-color-danger); font-weight: 600; }
.context-max { margin-left: auto; }

/* Messages */
.chat-messages {
  flex: 1;
  min-height: 120px;
  max-height: 300px;
  overflow-y: auto;
  padding: 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.chat-empty {
  text-align: center;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  padding: 20px 0;
}
.chat-empty-hint {
  font-size: 11px;
  margin-top: 4px;
}

.chat-message {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.msg-role {
  font-size: 11px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
}
.msg-content {
  font-size: 13px;
  line-height: 1.5;
  padding: 6px 10px;
  border-radius: 6px;
  background: var(--el-fill-color-light);
  white-space: pre-wrap;
  word-break: break-word;
}
.msg-user .msg-content {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary-dark-2);
}
.msg-system .msg-content {
  background: var(--el-color-danger-light-9);
  color: var(--el-color-danger);
}

.loading-dots {
  color: var(--el-text-color-placeholder);
  font-style: italic;
}

/* Input */
.chat-input-area {
  padding: 8px 10px;
  border-top: 1px solid var(--el-border-color-lighter);
  display: flex;
  gap: 6px;
  align-items: flex-end;
  flex-shrink: 0;
}
.chat-input-area .el-textarea {
  flex: 1;
}
</style>
