<script setup>
import { ref, nextTick, watch } from 'vue';
import { FullScreen, Location, Link, ArrowLeft, Refresh } from '@element-plus/icons-vue';
import { renderMarkdown } from '../composables/useMarkdown';
import { api } from '../services/api';
import NodeAiChatPanel from './NodeAiChatPanel.vue';
import RelationGraphCanvas from './RelationGraphCanvas.vue';

const props = defineProps({
  node: { type: Object, default: null },
  nodes: { type: Array, default: () => [] },
  relations: { type: Array, default: () => [] },
  currentMapId: { type: [Number, String], default: null },
  workMode: { type: String, default: 'display' },
  nodeForm: { type: Object, default: null },
  relationForm: { type: Object, default: null },
  candidateTargets: { type: Array, default: () => [] },
  selectedNodeRelations: { type: Array, default: () => [] },
  nodeTitleById: { type: Object, default: () => new Map() },
  loading: { type: Boolean, default: false },
  searchNodes: { type: Function, default: null }
});

const emit = defineEmits(['preview-node', 'jump-to-node', 'save-node', 'create-relation', 'delete-relation']);

const graphOpen = ref(false);
const currentNode = ref(null);
const currentRelations = ref([]);
const cardLoading = ref(false);
const navHistory = ref([]);
const searchKeyword = ref('');
const searchResults = ref([]);
const searching = ref(false);
const showRefDialog = ref(false);
const refTriggerPos = ref({ start: -1, end: -1 });

function mergeRelations(mapRelations, nodeRelations) {
  const relationById = new Map();
  [...mapRelations, ...nodeRelations].forEach((relation) => {
    relationById.set(relation.id ?? `${relation.sourceId}-${relation.targetId}-${relation.relationType}`, relation);
  });
  return [...relationById.values()];
}

watch(() => props.node, async (val) => {
  if (val) {
    navHistory.value = [val];
    await loadNodeData(val);
  } else {
    currentNode.value = null;
    currentRelations.value = [];
    navHistory.value = [];
  }
}, { immediate: true });

async function loadNodeData(node) {
  if (!node) return;
  currentNode.value = node;
  currentRelations.value = props.relations;
  cardLoading.value = true;
  try {
    const [nodeResult, relationResult] = await Promise.all([
      api(`/api/nodes/${node.id}`),
      api(`/api/node-relations/by-node/${node.id}`)
    ]);
    if (nodeResult) currentNode.value = nodeResult;
    if (relationResult) currentRelations.value = mergeRelations(props.relations, relationResult);
  } catch (err) {
    console.error('Failed to load node data:', err);
  } finally {
    cardLoading.value = false;
  }
}

async function refreshCard() {
  await loadNodeData(currentNode.value);
}

function navigateTo(node) {
  if (node.id === currentNode.value?.id) return;
  navHistory.value.push({ ...currentNode.value });
  loadNodeData(node);
}

function goBack() {
  if (navHistory.value.length > 0) {
    const prev = navHistory.value.pop();
    loadNodeData(prev);
  }
}

async function handleSearch(query) {
  if (!query) { searchResults.value = []; return; }
  searching.value = true;
  try {
    const results = await props.searchNodes(query);
    searchResults.value = results.filter(n => n.id !== props.node?.id);
  } finally { searching.value = false; }
}

function insertReference(node) {
  const refText = `[[${node.title}|${node.id}]]`;
  const content = props.nodeForm.content || '';
  if (refTriggerPos.value.start >= 0) {
    const before = content.slice(0, refTriggerPos.value.start);
    const after = content.slice(refTriggerPos.value.end);
    props.nodeForm.content = before + refText + after;
  } else {
    props.nodeForm.content = content + (content.length > 0 && !content.endsWith('\n') ? '\n' : '') + refText;
  }
  showRefDialog.value = false;
  searchKeyword.value = '';
  searchResults.value = [];
  refTriggerPos.value = { start: -1, end: -1 };
}

function onContentKeyup(event) {
  const el = event.target;
  if (!el) return;
  const pos = el.selectionStart;
  const text = el.value || '';
  if (pos >= 2 && text.slice(pos - 2, pos) === '[[') {
    refTriggerPos.value = { start: pos - 2, end: pos };
    showRefDialog.value = true;
  }
}

function onRefDialogOpened() {
  nextTick(() => {
    const input = document.querySelector('.ref-dialog-select-wrap .el-select__input');
    if (input) input.focus();
  });
}

function handleContentClick(event) {
  const target = event.target;
  if (target.tagName === 'A' && target.classList.contains('node-ref')) {
    event.preventDefault();
    const id = Number(target.getAttribute('data-id'));
    let targetNode = props.nodes.find(n => n.id === id);
    if (!targetNode) {
      const rel = currentRelations.value.find(r => r.sourceId === id || r.targetId === id);
      targetNode = {
        id, title: rel ? (rel.sourceId === id ? rel.sourceTitle : rel.targetTitle) : `节点 #${id}`,
        isExternal: true
      };
    }
    if (targetNode) navigateTo(targetNode);
  }
}

function jumpToMap() {
  if (currentNode.value) {
    emit('jump-to-node', { mapId: currentNode.value.mapId, nodeId: currentNode.value.id });
  }
}
</script>

<template>
  <div class="knowledge-card-wrapper">
    <NodeAiChatPanel :node="currentNode" :current-map-id="currentMapId" />
    <aside class="knowledge-card" v-loading="cardLoading">

    <!-- === DISPLAY MODE === -->
    <template v-if="workMode === 'display'">
      <div class="section-heading">
        <div style="display:flex;align-items:center;gap:6px;min-width:0;">
          <el-button v-if="navHistory.length > 0" :icon="ArrowLeft" size="small" text @click="goBack" />
          <h2>{{ currentNode?.title ?? '节点内容' }}</h2>
        </div>
        <div class="card-heading-actions">
          <el-tag v-if="currentNode?.isExternal || (currentNode?.mapId && String(currentNode.mapId) !== String(props.currentMapId))" type="info" size="small" effect="plain">跨图</el-tag>
          <el-button
            size="small"
            :icon="Refresh"
            :disabled="!currentNode || cardLoading"
            data-testid="refresh-knowledge-card"
            @click="refreshCard"
          >
            刷新
          </el-button>
        </div>
      </div>
      <div v-if="!currentNode" class="empty small">选择一个节点查看详情。</div>
      <template v-else>
        <div class="card-content">
          <div v-if="currentNode.content" class="markdown-body" @click="handleContentClick" v-html="renderMarkdown(currentNode.content)"></div>
          <p v-else class="muted">该节点暂无内容。</p>
          <div class="node-meta" v-if="!currentNode.isExternal">
            <span>编号 #{{ currentNode.id }}</span>
            <span v-if="currentNode.mapTitle">· {{ currentNode.mapTitle }}</span>
            <span v-if="currentNode.orderNo != null">· 排序 {{ currentNode.orderNo }}</span>
          </div>
          <div class="jump-link" v-if="currentNode.mapId && String(currentNode.mapId) !== String(props.currentMapId)">
            <el-button link type="primary" :icon="Location" size="small" @click="jumpToMap">跳转至导图</el-button>
          </div>
        </div>
        <section class="relation-section">
          <div class="relation-section-heading">
            <span>关联图谱</span>
            <el-button :icon="FullScreen" size="small" :disabled="!currentNode" @click="graphOpen = true">详情大图</el-button>
          </div>
          <RelationGraphCanvas
            :center-node="currentNode"
            :nodes="nodes"
            :relations="currentRelations"
            :height="240"
            :show-labels="false"
            :node-draggable="false"
            @preview-node="(n) => navigateTo(n)"
          />
        </section>
      </template>
    </template>

    <!-- === WORKBENCH MODE === -->
    <template v-else-if="workMode === 'workbench'">
      <div class="section-heading">
        <h2>{{ currentNode?.title ?? '节点编辑' }}</h2>
        <span>{{ currentNode ? `#${currentNode.id}` : '未选择' }}</span>
      </div>
      <div v-if="!currentNode" class="empty small">请在画布或列表中选择一个节点。</div>
      <template v-else>
        <div class="card-editor">
          <label>节点标题<el-input v-model="nodeForm.title" placeholder="节点标题" /></label>
          <label>
            <div style="display:flex;justify-content:space-between;align-items:center;">
              <span>节点内容</span>
              <el-button link type="primary" size="small" :icon="Link" @click="showRefDialog = true">插入引用</el-button>
            </div>
            <el-input v-model="nodeForm.content" type="textarea" :rows="4" placeholder="节点内容。输入 [[ 快捷引用。" @keyup="onContentKeyup" />
          </label>
          <label>同级排序<el-input-number v-model="nodeForm.orderNo" :min="0" style="width:100%;" /></label>
          <el-button type="primary" :disabled="loading" @click="$emit('save-node')">保存当前节点</el-button>
        </div>
      </template>
    </template>
  </aside>
  </div>

  <!-- [[ reference dialog -->
  <el-dialog v-model="showRefDialog" title="插入节点引用 (全局)" width="480px" append-to-body @opened="onRefDialogOpened">
    <el-select v-model="searchKeyword" filterable remote reserve-keyword placeholder="输入关键词搜索全库节点" :remote-method="handleSearch" :loading="searching" style="width:100%" class="ref-dialog-select-wrap" popper-class="ref-dialog-select" @change="(id) => { const n = searchResults.find(r => r.id === id); if (n) insertReference(n); }">
      <el-option v-for="item in searchResults" :key="item.id" :label="item.title" :value="item.id">
        <el-tooltip effect="dark" placement="right" :show-after="300">
          <template #content>
            <div class="search-preview-tooltip">
              <div class="tooltip-map-tag" v-if="item.mapTitle">所属导图：{{ item.mapTitle }}</div>
              <div v-if="item.content" class="markdown-body mini" v-html="renderMarkdown(item.content)"></div>
              <div v-else class="muted">暂无详细内容</div>
            </div>
          </template>
          <div style="display:flex;justify-content:space-between;align-items:center;width:100%;">
            <span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:240px;">{{ item.title }}</span>
            <span style="color:var(--el-text-color-secondary);font-size:12px;margin-left:8px;">#{{ item.id }}</span>
          </div>
        </el-tooltip>
      </el-option>
    </el-select>
    <template #footer><el-button @click="showRefDialog = false">取消</el-button></template>
  </el-dialog>

  <!-- relation graph enlarge dialog -->
  <el-dialog v-model="graphOpen" title="关联图谱" width="min(1080px, calc(100vw - 32px))" class="relation-graph-dialog">
    <RelationGraphCanvas :center-node="currentNode" :nodes="nodes" :relations="currentRelations" :height="620" :node-draggable="false" @preview-node="(n) => navigateTo(n)" />
  </el-dialog>
</template>

<style scoped>
.knowledge-card-wrapper { position: relative; height: calc(100vh - 136px); }
.knowledge-card { display:flex; flex-direction:column; padding:12px; background:#fff; border:1px solid #d8e0e8; border-radius:8px; overflow:hidden; height:100%; gap:8px; }
.section-heading { display:flex; justify-content:space-between; align-items:center; gap:8px; flex-shrink:0; }
.section-heading h2 { margin:0; font-size:15px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.section-heading span { font-size:12px; color:var(--el-text-color-secondary); white-space:nowrap; }
.card-heading-actions { display:flex; align-items:center; gap:6px; flex-shrink:0; }
.card-content, .card-editor { flex:1; min-height:0; overflow-y:auto; display:flex; flex-direction:column; gap:8px; }
.card-content .markdown-body { color:#263747; line-height:1.6; font-size:13px; }
.card-editor label { display:flex; flex-direction:column; gap:3px; font-size:13px; font-weight:500; }
.node-meta { display:flex; flex-wrap:wrap; gap:6px; font-size:12px; color:var(--el-text-color-secondary); }
.relation-section { flex-shrink:0; display:flex; flex-direction:column; gap:6px; border-top:1px solid var(--el-border-color-lighter); padding-top:8px; }
.relation-section-heading { display:flex; justify-content:space-between; align-items:center; font-size:13px; font-weight:600; color:var(--el-text-color-primary); }
.muted { color:var(--el-text-color-secondary); font-size:13px; }
.empty.small { text-align:center; color:var(--el-text-color-placeholder); padding:12px; font-size:13px; border:1px dashed var(--el-border-color); border-radius:6px; }
</style>
