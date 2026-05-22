<script setup>
import { ChatDotRound, Clock, Download, MagicStick, Upload } from '@element-plus/icons-vue';
import { renderMarkdown } from '../composables/useMarkdown';
import MapSidebar from './MapSidebar.vue';
import MindMapCanvas from './MindMapCanvas.vue';
import NodeTreeView from './NodeTreeView.vue';

const props = defineProps({
  workspace: { type: Object, required: true },
  viewMode: { type: String, required: true }
});

const emit = defineEmits(['created', 'update:viewMode', 'preview-node']);

function selectMap(id) {
  props.workspace.selectMap(id);
}
</script>

<template>
  <section class="create-page">
    <div class="create-with-sidebar">
      <MapSidebar
        :maps="workspace.maps.value"
        :selected-map-id="workspace.selectedMapId.value"
        :loading="workspace.loading.value"
        :deletable="false"
        @select-map="selectMap"
      />
      <div class="create-main">
        <div class="create-grid">

          <section class="panel create-card">
            <div class="section-heading">
              <h2>AI 清洗</h2>
              <span class="current-model-label">模型：{{ workspace.aiModels.value.find(m => m.id === workspace.selectedAiModelId.value)?.name || '未选择' }}</span>
            </div>
            <div class="field-row ai-actions">
              <el-button :icon="MagicStick" data-testid="ai-clean" :loading="workspace.loading.value" @click="workspace.cleanNaturalLanguage">自然语言清洗</el-button>
              <el-button :icon="ChatDotRound" data-testid="ai-open-chat" @click="workspace.chatOpen.value = true">需求对话</el-button>
            </div>
            <p v-if="workspace.aiStatus.value" class="inline-status" data-testid="ai-status">{{ workspace.aiStatus.value }}</p>
            <el-input v-model="workspace.naturalLanguageInput.value" data-testid="ai-natural-language" type="textarea" :rows="14" placeholder="请输入自然语言描述，AI 会扩充为标准导图结构 JSON。" />
          </section>

          <section class="panel create-card">
            <div class="section-heading">
              <h2>导入 / 导出</h2>
              <span>JSON</span>
            </div>
            <div class="transfer-actions">
              <el-button :icon="Download" data-testid="export-structure" :disabled="!workspace.selectedMap.value" @click="workspace.exportStructure">导出结构</el-button>
              <el-button :icon="Download" data-testid="export-file" :disabled="!workspace.selectedMap.value" @click="workspace.downloadSelectedMap">导出文件</el-button>
              <el-button data-testid="download-template" @click="workspace.downloadUrl('/api/mind-map-transfer/template')">模板</el-button>
              <el-button :icon="Upload" data-testid="import-structure" @click="workspace.importStructure">导入结构</el-button>
            </div>
            <div class="field-row transfer-import-row">
              <el-input v-model="workspace.importTitleOverride.value" data-testid="import-title" placeholder="可选：导入后的标题" />
              <input :ref="(el) => { workspace.fileInput.value = el; }" data-testid="import-file" type="file" accept="application/json,.json" @change="workspace.importFile" />
            </div>
            <el-input v-model="workspace.transferText.value" data-testid="transfer-text" type="textarea" :rows="12" placeholder="导出的结构会显示在这里，也可以粘贴模板或导图 JSON 后导入。" />
          </section>
        </div>

        <section class="panel create-preview-panel">
          <div class="section-heading">
            <h2>节点展示</h2>
            <el-segmented :model-value="viewMode" :options="[{label:'图',value:'graph'},{label:'列表',value:'list'}]" @update:model-value="$emit('update:viewMode', $event)" />
          </div>
          <MindMapCanvas
            v-if="viewMode === 'graph'"
            :map="workspace.selectedMap.value"
            :nodes="workspace.nodes.value"
            :relations="workspace.relations.value"
            :selected-node-id="workspace.selectedNodeId.value"
            @select-node="workspace.selectNode"
            @preview-node="$emit('preview-node', $event)"
          />
          <NodeTreeView
            v-else
            :map="workspace.selectedMap.value"
            :nodes="workspace.nodes.value"
            :selected-node-id="workspace.selectedNodeId.value"
            @select-node="workspace.selectNode"
            @preview-node="$emit('preview-node', $event)"
          />
        </section>
      </div>
    </div>

    <el-dialog v-model="workspace.chatOpen.value" title="需求对话" width="min(760px, calc(100vw - 32px))" class="chat-dialog">
      <div class="chat-log">
        <div v-if="workspace.chatMessages.value.length === 0" class="empty small">本次对话还没有消息。</div>
        <div v-for="(message, index) in workspace.chatMessages.value" :key="index" class="chat-message" :class="message.role">
          <strong>{{ message.role === 'user' ? '你' : 'AI' }}</strong>
          <div class="markdown-body" v-html="renderMarkdown(message.content)"></div>
        </div>
      </div>
      <el-input v-model="workspace.chatInput.value" data-testid="ai-chat-input" type="textarea" :rows="4" placeholder="围绕需求继续对话，本次对话记录会作为程序管理的上下文。" />
      <template #footer>
        <div class="chat-dialog-footer">
          <el-button :icon="Clock" data-testid="ai-chat-history-footer" @click="workspace.loadConversationHistory">历史对话</el-button>
          <el-button data-testid="ai-new-chat" @click="workspace.startNewConversation">新对话</el-button>
          <el-button data-testid="ai-chat-clean" :disabled="workspace.chatMessages.value.length === 0" @click="workspace.cleanConversationContext">生成结构体</el-button>
          <el-button type="primary" data-testid="ai-chat-send" :loading="workspace.loading.value" @click="workspace.sendChatMessage">发送</el-button>
        </div>
      </template>
    </el-dialog>

    <el-dialog v-model="workspace.chatHistoryOpen.value" title="历史对话" width="min(640px, calc(100vw - 32px))" class="chat-history-dialog">
      <div v-loading="workspace.chatHistoryLoading.value" class="chat-history-list">
        <div v-if="workspace.chatHistoryGroups.value.length === 0" class="empty small">暂无历史对话。</div>
        <button v-for="group in workspace.chatHistoryGroups.value" :key="group.conversationId" type="button" class="chat-history-item" data-testid="ai-chat-history-item" @click="workspace.restoreConversation(group)">
          <span>{{ group.title }}</span>
          <small>{{ group.count }} 条消息 · {{ group.updatedAt ? new Date(group.updatedAt).toLocaleString() : '' }}</small>
        </button>
      </div>
    </el-dialog>
  </section>
</template>

<style scoped>
.create-with-sidebar {
  display: grid;
  grid-template-columns: 280px minmax(0, 1fr);
  gap: 14px;
  align-items: start;
}
.create-main {
  display: flex;
  flex-direction: column;
  gap: 14px;
  min-width: 0;
}
.create-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(360px, 1fr));
  gap: 14px;
  align-items: stretch;
}
</style>
