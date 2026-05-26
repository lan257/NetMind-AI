<script setup>
import { ref, computed, watch, nextTick } from 'vue';
import { ElMessageBox } from 'element-plus';
import { ChatDotRound, Clock, Plus, ArrowRight, Check, Close } from '@element-plus/icons-vue';
import { useNodeAiChat } from '../composables/useNodeAiChat';
import { renderMarkdown } from '../composables/useMarkdown';

const props = defineProps({
  node: { type: Object, default: null },
  currentMapId: { type: [Number, String], default: null }
});

const chat = useNodeAiChat();

const collapsed = ref(true);

const chatModes = [
  { value: 'node-agent', label: '节点问答', icon: ChatDotRound, available: true },
  { value: 'map-agent', label: '全图问答', icon: ChatDotRound, available: true },
  { value: 'global', label: '全局问答', icon: ChatDotRound, available: true },
  { value: 'app-help', label: '应用帮助', icon: ChatDotRound, available: true }
];

const APP_HELP_INTRO = '你好！我是 NetMind 应用帮助 Agent。我可以帮你了解功能、操作、部署和排障；对话中学到的新经验会追加到学习记录，正式说明书由管理员统一维护。';

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

function selectMode(mode) {
  if (!mode.available) return;
  chat.chatMode.value = mode.value;
  chat.clearChat();

  // Show intro message for app-help mode
  if (mode.value === 'app-help') {
    chat.messages.value.push({ role: 'assistant', content: APP_HELP_INTRO });
  }
}

function requiresNodeMode(mode) {
  return mode === 'node-agent';
}

function requiresMapMode(mode) {
  return mode === 'map-agent';
}

const inputDisabled = computed(() => {
  return (requiresNodeMode(chat.chatMode.value) && !props.node) ||
    (requiresMapMode(chat.chatMode.value) && !props.currentMapId) ||
    chat.loading.value;
});

async function handleSend() {
  if (!chat.inputText.value.trim() || chat.loading.value) return;

  if (requiresNodeMode(chat.chatMode.value) && !props.node) return;
  if (requiresMapMode(chat.chatMode.value) && !props.currentMapId) return;

  await chat.sendMessage(props.node, props.currentMapId);
  nextTick(() => scrollToBottom());
}

async function handleToolApproval(call, approved) {
  let rejectReason = '';
  if (!approved) {
    try {
      const { value } = await ElMessageBox.prompt(
        '可填写拒绝原因，AI 会在下一轮根据原因继续回答。',
        '拒绝工具调用',
        {
          confirmButtonText: '提交拒绝',
          cancelButtonText: '取消',
          inputPlaceholder: '例如：该文件包含隐私内容，不允许读取。'
        }
      );
      rejectReason = value || '';
    } catch {
      return;
    }
  }

  await chat.confirmToolCall(call, approved, props.node, props.currentMapId, rejectReason);
  nextTick(() => scrollToBottom());
}

function handleKeyup(event) {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault();
    handleSend();
  }
}

// Clear chat when node changes (only in node mode)
watch(() => props.node?.id, () => {
  if (chat.chatMode.value === 'node-agent') {
    chat.clearChat();
  }
});

// Show intro when switching to app-help mode
watch(() => chat.chatMode.value, (newMode) => {
  if (newMode === 'app-help' && chat.messages.value.length === 0) {
    chat.messages.value.push({ role: 'assistant', content: APP_HELP_INTRO });
  }
});

const currentModeLabel = computed(() => {
  const m = chatModes.find(m => m.value === chat.chatMode.value);
  return m ? m.label : chat.chatMode.value;
});

function getToolName(call) {
  return call?.tool_name ||
    call?.toolName ||
    call?.tool_id ||
    call?.toolId ||
    '工具';
}

function getToolReason(call) {
  return call?.reason ||
    call?.permission?.reject_reason ||
    call?.execution?.denied_reason ||
    call?.execution?.error ||
    '';
}

function getToolStatus(call) {
  return call?.execution?.status || '';
}

function getAgentStatusText(status) {
  const map = {
    waiting_permission: '等待确认',
    running: '执行中',
    planning: '规划中'
  };
  return map[status] || status || '进行中';
}

function getToolStatusText(call) {
  const status = getToolStatus(call);
  const map = {
    waiting_permission: '待确认',
    permission_approved: '已允许',
    permission_denied: '已拒绝',
    ready: '待执行',
    running: '执行中',
    success: '已完成',
    failed: '失败'
  };
  return map[status] || status || '未知';
}

function getToolStatusType(call) {
  const status = getToolStatus(call);
  if (status === 'success') return 'success';
  if (status === 'waiting_permission') return 'warning';
  if (status === 'permission_approved') return 'success';
  if (status === 'failed' || status === 'permission_denied') return 'danger';
  return 'info';
}

function getToolStatusClass(call) {
  const status = getToolStatus(call);
  if (status === 'permission_approved' || status === 'success') return 'status-approved';
  if (status === 'permission_denied' || status === 'failed') return 'status-denied';
  return '';
}

function getPermissionMessage(call) {
  return call?.permission?.message || '是否允许执行该工具？';
}

function isWaitingPermission(call) {
  return getToolStatus(call) === 'waiting_permission';
}
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
        <el-select
          :model-value="chat.chatMode.value"
          class="chat-mode-select"
          size="small"
          popper-class="chat-mode-popper"
          @change="(val) => { const m = chatModes.find(cm => cm.value === val); if (m) selectMode(m); }"
        >
          <el-option
            v-for="m in chatModes"
            :key="m.value"
            :label="m.label + (m.available ? '' : '（待实现）')"
            :value="m.value"
            :disabled="!m.available"
          />
        </el-select>
        <el-button :icon="Clock" size="small" text title="历史对话" @click="chat.loadHistory()" />
        <el-button :icon="Plus" size="small" text title="新对话" @click="chat.startNewConversation()" />
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
          <template v-if="chat.chatMode.value === 'node-agent'">
            <p>节点问答</p>
            <p class="chat-empty-hint">通过 AgentBuild 内核进行节点问答和工具调用</p>
            <p class="chat-empty-hint" v-if="!node">请先在画布或列表中选择一个节点</p>
          </template>
          <template v-else-if="chat.chatMode.value === 'map-agent'">
            <p>全图问答</p>
            <p class="chat-empty-hint">通过 AgentBuild 内核读取当前导图全量数据并回答</p>
            <p class="chat-empty-hint" v-if="!currentMapId">请先在侧边栏选择一个思维导图</p>
          </template>
          <template v-else-if="chat.chatMode.value === 'global'">
            <p>全局问答</p>
            <p class="chat-empty-hint">仅基于基础信息、对话历史和 Agent 记忆回答</p>
          </template>
          <!-- App help mode -->
          <template v-else-if="chat.chatMode.value === 'app-help'">
            <p>应用帮助 Agent</p>
            <p class="chat-empty-hint">回答使用问题，并把经验追加到学习记录</p>
          </template>
          <!-- Other placeholder modes -->
          <template v-else>
            <p>{{ currentModeLabel }}</p>
            <p class="chat-empty-hint">该模式尚未实现，敬请期待</p>
          </template>
        </div>
        <div v-for="(msg, idx) in chat.messages.value" :key="idx" :class="['chat-message', `msg-${msg.role}`, msg.tone ? `tone-${msg.tone}` : '']">
          <div class="msg-role">{{ msg.role === 'user' ? '你' : msg.role === 'assistant' ? 'AI' : '系统' }}</div>
          <div v-if="msg.agent?.progressText" class="agent-progress">
            <div class="agent-progress-head">
              <span>Agent 进度</span>
              <el-tag size="small" type="warning">{{ getAgentStatusText(msg.agent.status) }}</el-tag>
            </div>
            <div v-if="msg.agent.agentTarget" class="agent-progress-target">{{ msg.agent.agentTarget }}</div>
            <div class="agent-progress-text markdown-body" v-html="renderMarkdown(msg.agent.progressText)"></div>
          </div>
          <div v-if="msg.content" class="msg-content markdown-body" v-html="renderMarkdown(msg.content)"></div>
          <div v-if="msg.agent?.toolCalls?.length" class="agent-tool-list">
            <div v-for="call in msg.agent.toolCalls" :key="call.call_id || call.callId || call.tool_id || call.toolId" :class="['agent-tool-item', getToolStatusClass(call)]">
              <div class="agent-tool-head">
                <span>{{ getToolName(call) }}</span>
                <el-tag size="small" :type="getToolStatusType(call)">{{ getToolStatusText(call) }}</el-tag>
              </div>
              <div v-if="getToolReason(call)" class="agent-tool-reason">{{ getToolReason(call) }}</div>
              <div v-if="isWaitingPermission(call)" class="agent-permission">
                <p>{{ getPermissionMessage(call) }}</p>
                <div class="agent-permission-actions">
                  <el-button :icon="Check" size="small" type="primary" :disabled="chat.loading.value" @click="handleToolApproval(call, true)">允许</el-button>
                  <el-button :icon="Close" size="small" :disabled="chat.loading.value" @click="handleToolApproval(call, false)">拒绝</el-button>
                </div>
              </div>
            </div>
          </div>
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
          :disabled="inputDisabled"
          @keyup="handleKeyup"
        />
        <el-button
          type="primary"
          :icon="ChatDotRound"
          :disabled="!chat.inputText.value.trim() || inputDisabled"
          :loading="chat.loading.value"
          size="small"
          @click="handleSend"
        >
          发送
        </el-button>
      </div>
    </div>

    <!-- History dialog -->
    <el-dialog v-model="chat.historyOpen.value" title="历史对话" width="380px" append-to-body :z-index="3000">
      <div v-loading="chat.historyLoading.value" class="chat-history-list">
        <div v-if="chat.historyError.value" class="history-error">{{ chat.historyError.value }}</div>
        <div v-else-if="chat.historyGroups.value.length === 0 && !chat.historyLoading.value" class="empty small">暂无历史对话。</div>
        <button
          v-for="group in chat.historyGroups.value"
          :key="group.conversationId"
          type="button"
          class="chat-history-item"
          @click="chat.restoreConversation(group)"
        >
          <span>{{ group.title }}</span>
          <small>{{ group.count }} 条消息 · {{ group.updatedAt ? new Date(group.updatedAt).toLocaleString() : '' }}</small>
        </button>
      </div>
    </el-dialog>
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
  height: min(520px, calc(100vh - 156px));
  min-width: 300px;
  max-width: min(720px, calc(100vw - 32px));
  min-height: 320px;
  max-height: calc(100vh - 156px);
  display: flex;
  flex-direction: column;
  background: #fff;
  border: 1px solid #d8e0e8;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0,0,0,0.08);
  overflow: hidden;
  resize: both;
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

.chat-mode-select {
  flex: 1;
  min-width: 0;
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

.msg-system.tone-success .msg-content {
  background: var(--el-color-success-light-9);
  color: var(--el-color-success);
}

.msg-system.tone-danger .msg-content {
  background: var(--el-color-danger-light-9);
  color: var(--el-color-danger);
}

.agent-progress {
  padding: 7px 8px;
  border: 1px solid var(--el-color-warning-light-5);
  border-radius: 6px;
  background: var(--el-color-warning-light-9);
}

.agent-progress-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.agent-progress-target {
  margin-top: 4px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.agent-progress-text {
  margin-top: 5px;
  font-size: 13px;
  line-height: 1.5;
  color: var(--el-text-color-regular);
}

.agent-tool-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.agent-tool-item {
  padding: 7px 8px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  background: var(--el-fill-color-lighter);
}

.agent-tool-item.status-approved {
  border-color: var(--el-color-success-light-7);
  background: var(--el-color-success-light-9);
}

.agent-tool-item.status-denied {
  border-color: var(--el-color-danger-light-7);
  background: var(--el-color-danger-light-9);
}

.agent-tool-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.agent-tool-item.status-approved .agent-tool-head {
  color: var(--el-color-success);
}

.agent-tool-item.status-denied .agent-tool-head {
  color: var(--el-color-danger);
}

.agent-tool-reason {
  margin-top: 4px;
  font-size: 12px;
  line-height: 1.4;
  color: var(--el-text-color-secondary);
}

.agent-permission {
  margin-top: 6px;
  padding-top: 6px;
  border-top: 1px dashed var(--el-border-color);
}

.agent-permission p {
  margin: 0 0 6px;
  font-size: 12px;
  line-height: 1.4;
  color: var(--el-text-color-regular);
}

.agent-permission-actions {
  display: flex;
  gap: 6px;
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

/* History dialog */
.chat-history-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 360px;
  overflow-y: auto;
}

.chat-history-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  width: 100%;
  padding: 8px 10px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  background: var(--el-fill-color-lighter);
  cursor: pointer;
  text-align: left;
  transition: background 0.15s;
  font-family: inherit;
  font-size: 13px;
  line-height: 1.4;
}
.chat-history-item:hover {
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary-light-5);
}
.chat-history-item span {
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.chat-history-item small {
  color: var(--el-text-color-secondary);
  font-size: 11px;
}

.empty.small {
  text-align: center;
  color: var(--el-text-color-placeholder);
  padding: 12px;
  font-size: 13px;
  border: 1px dashed var(--el-border-color);
  border-radius: 6px;
}

.history-error {
  text-align: center;
  color: var(--el-color-danger);
  padding: 12px;
  font-size: 13px;
  background: var(--el-color-danger-light-9);
  border-radius: 6px;
  line-height: 1.5;
}
</style>
