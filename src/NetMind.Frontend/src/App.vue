<script setup>
import { computed, onMounted, ref } from 'vue';

const maps = ref([]);
const nodes = ref([]);
const relations = ref([]);
const selectedMapId = ref(null);
const selectedNodeId = ref(null);
const mapTitle = ref('');
const nodeForm = ref({ title: '', content: '', orderNo: 1 });
const relationForm = ref({ targetId: '', relationType: 'relates_to', weight: 1 });
const transferText = ref('');
const importTitleOverride = ref('');
const fileInput = ref(null);
const aiModels = ref([]);
const selectedAiModelId = ref('');
const naturalLanguageInput = ref('');
const aiStatus = ref('');
const chatOpen = ref(false);
const chatInput = ref('');
const chatMessages = ref([]);
const chatConversationId = ref(createConversationId());
const loading = ref(false);
const errorMessage = ref('');
const noticeMessage = ref('');

const selectedMap = computed(() => maps.value.find((map) => map.id === selectedMapId.value) ?? null);
const selectedNode = computed(() => nodes.value.find((node) => node.id === selectedNodeId.value) ?? null);
const candidateTargets = computed(() => nodes.value.filter((node) => node.id !== selectedNodeId.value));
const selectedNodeRelations = computed(() => {
  if (!selectedNode.value) {
    return [];
  }

  return relations.value.filter(
    (relation) => relation.sourceId === selectedNode.value.id || relation.targetId === selectedNode.value.id
  );
});
const nodeTitleById = computed(() => {
  const result = new Map();
  nodes.value.forEach((node) => result.set(node.id, node.title));
  return result;
});
const childCountByParent = computed(() => {
  const counts = new Map();
  nodes.value.forEach((node) => {
    const key = node.parentId ?? 0;
    counts.set(key, (counts.get(key) ?? 0) + 1);
  });
  return counts;
});
const visualNodes = computed(() => {
  const byParent = new Map();
  nodes.value.forEach((node) => {
    const key = node.parentId ?? 0;
    if (!byParent.has(key)) {
      byParent.set(key, []);
    }
    byParent.get(key).push(node);
  });

  byParent.forEach((items) => {
    items.sort((left, right) => left.orderNo - right.orderNo || left.id - right.id);
  });

  const result = [];
  const walk = (parentId, depth) => {
    (byParent.get(parentId) ?? []).forEach((node) => {
      result.push({ ...node, depth, childCount: childCountByParent.value.get(node.id) ?? 0 });
      walk(node.id, depth + 1);
    });
  };

  walk(0, 0);
  return result;
});
const chatContextText = computed(() => buildConversationContext());

function createConversationId() {
  if (window.crypto?.randomUUID) {
    return window.crypto.randomUUID();
  }

  return `chat-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

async function api(path, options = {}) {
  const headers = { ...(options.headers ?? {}) };
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(path, { ...options, headers });
  const text = await response.text();
  let result = {};
  try {
    result = text ? JSON.parse(text) : {};
  } catch {
    throw new Error(text || `请求失败：${response.status}`);
  }

  if (!response.ok || !result.success) {
    throw new Error(result.message || `请求失败：${response.status}`);
  }

  return result.data;
}

async function run(action, successMessage = '') {
  loading.value = true;
  errorMessage.value = '';
  noticeMessage.value = '';

  try {
    const result = await action();
    if (successMessage) {
      noticeMessage.value = successMessage;
    }
    return result;
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '操作失败';
    return null;
  } finally {
    loading.value = false;
  }
}

function resetNodeForm() {
  nodeForm.value = { title: '', content: '', orderNo: 1 };
}

function fillNodeForm(node) {
  if (!node) {
    resetNodeForm();
    return;
  }

  nodeForm.value = {
    title: node.title,
    content: node.content ?? '',
    orderNo: node.orderNo
  };
}

async function refreshMapData(mapId, options = {}) {
  const keepNodeId = options.keepNodeId ?? selectedNodeId.value;
  const result = await run(async () => {
    const [nodeData, relationData] = await Promise.all([
      api(`/api/nodes/by-map/${mapId}`),
      api(`/api/node-relations/by-map/${mapId}`)
    ]);
    return { nodeData, relationData };
  }, options.message ?? '');

  if (!result) {
    nodes.value = [];
    relations.value = [];
    selectedNodeId.value = null;
    resetNodeForm();
    return;
  }

  nodes.value = result.nodeData;
  relations.value = result.relationData;

  if (keepNodeId && nodes.value.some((node) => node.id === keepNodeId)) {
    selectedNodeId.value = keepNodeId;
    fillNodeForm(selectedNode.value);
  } else {
    selectedNodeId.value = null;
    resetNodeForm();
  }
}

async function loadMaps() {
  const data = await run(() => api('/api/mind-maps'), '导图已刷新');
  if (!data) {
    return;
  }

  maps.value = data;
  if (maps.value.length === 0) {
    selectedMapId.value = null;
    mapTitle.value = '';
    nodes.value = [];
    relations.value = [];
    selectedNodeId.value = null;
    resetNodeForm();
    return;
  }

  const nextMap = maps.value.find((map) => map.id === selectedMapId.value) ?? maps.value[0];
  await selectMap(nextMap.id);
}

async function loadAiModels() {
  const data = await run(() => api('/api/ai/models'));
  if (!data) {
    return;
  }

  aiModels.value = data;
  selectedAiModelId.value = data.find((model) => model.isDefault)?.id ?? data[0]?.id ?? '';
}

async function selectMap(id) {
  selectedMapId.value = id;
  const map = maps.value.find((item) => item.id === id);
  mapTitle.value = map?.title ?? '';
  await refreshMapData(id, { keepNodeId: null, message: '导图已加载' });
}

async function createMap() {
  const title = mapTitle.value.trim();
  if (!title) {
    errorMessage.value = '请输入导图标题';
    return;
  }

  const created = await run(
    () => api('/api/mind-maps', { method: 'POST', body: JSON.stringify({ title }) }),
    '导图已创建'
  );

  if (created) {
    await loadMaps();
    await selectMap(created.id);
  }
}

async function renameMap() {
  if (!selectedMap.value) {
    errorMessage.value = '请先选择导图';
    return;
  }

  const title = mapTitle.value.trim();
  if (!title) {
    errorMessage.value = '请输入新的导图标题';
    return;
  }

  const updated = await run(
    () => api(`/api/mind-maps/${selectedMap.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({ title, rootNodeId: selectedMap.value.rootNodeId })
    }),
    '导图已重命名'
  );

  if (updated) {
    await loadMaps();
  }
}

async function deleteMap(cascade) {
  if (!selectedMap.value) {
    errorMessage.value = '请先选择导图';
    return;
  }

  const deletedId = selectedMap.value.id;
  const deleted = await run(
    () => api(`/api/mind-maps/${deletedId}${cascade ? '/cascade' : ''}`, { method: 'DELETE' }),
    cascade ? '导图及内容已删除' : '导图已删除'
  );

  if (deleted) {
    selectedMapId.value = null;
    await loadMaps();
  }
}

function selectNode(id) {
  selectedNodeId.value = id;
  fillNodeForm(selectedNode.value);
}

async function createNode(parentId = null) {
  if (!selectedMap.value) {
    errorMessage.value = '请先选择导图';
    return;
  }

  const title = nodeForm.value.title.trim();
  if (!title) {
    errorMessage.value = '请输入节点标题';
    return;
  }

  const created = await run(
    () => api('/api/nodes', {
      method: 'POST',
      body: JSON.stringify({
        mapId: selectedMap.value.id,
        parentId,
        title,
        content: nodeForm.value.content,
        orderNo: Number(nodeForm.value.orderNo) || 0
      })
    }),
    '节点已创建'
  );

  if (created) {
    await refreshMapData(selectedMap.value.id, { keepNodeId: created.id });
  }
}

async function updateNode() {
  if (!selectedNode.value) {
    errorMessage.value = '请先选择节点';
    return;
  }

  const title = nodeForm.value.title.trim();
  if (!title) {
    errorMessage.value = '请输入节点标题';
    return;
  }

  const updated = await run(
    () => api(`/api/nodes/${selectedNode.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        parentId: selectedNode.value.parentId,
        title,
        content: nodeForm.value.content,
        orderNo: Number(nodeForm.value.orderNo) || 0
      })
    }),
    '节点已保存'
  );

  if (updated) {
    await refreshMapData(selectedMap.value.id, { keepNodeId: updated.id });
  }
}

async function deleteNode(subtree) {
  if (!selectedNode.value) {
    errorMessage.value = '请先选择节点';
    return;
  }

  const deletedNodeId = selectedNode.value.id;
  const deleted = await run(
    () => api(`/api/nodes/${deletedNodeId}${subtree ? '/subtree' : ''}`, { method: 'DELETE' }),
    subtree ? '节点子树已删除' : '节点已删除'
  );

  if (deleted) {
    await refreshMapData(selectedMap.value.id, { keepNodeId: null });
  }
}

async function createRelation() {
  if (!selectedMap.value || !selectedNode.value) {
    errorMessage.value = '请先选择导图和源节点';
    return;
  }

  const targetId = Number(relationForm.value.targetId);
  if (!targetId) {
    errorMessage.value = '请选择目标节点';
    return;
  }

  const created = await run(
    () => api('/api/node-relations', {
      method: 'POST',
      body: JSON.stringify({
        mapId: selectedMap.value.id,
        sourceId: selectedNode.value.id,
        targetId,
        relationType: relationForm.value.relationType.trim() || 'relates_to',
        weight: Number(relationForm.value.weight) || 0
      })
    }),
    '关联已创建'
  );

  if (created) {
    relationForm.value = { targetId: '', relationType: 'relates_to', weight: 1 };
    await refreshMapData(selectedMap.value.id, { keepNodeId: selectedNodeId.value });
  }
}

async function deleteRelation(id) {
  const deleted = await run(() => api(`/api/node-relations/${id}`, { method: 'DELETE' }), '关联已删除');
  if (deleted) {
    await refreshMapData(selectedMap.value.id, { keepNodeId: selectedNodeId.value });
  }
}

async function exportStructure() {
  if (!selectedMap.value) {
    errorMessage.value = '请先选择导图';
    return;
  }

  const structure = await run(
    () => api(`/api/mind-map-transfer/${selectedMap.value.id}/structure`),
    '结构体已导出'
  );
  if (structure) {
    transferText.value = JSON.stringify(structure.transfer, null, 2);
  }
}

function downloadUrl(url) {
  window.location.href = url;
}

function downloadSelectedMap() {
  if (!selectedMap.value) {
    errorMessage.value = '请先选择导图';
    return;
  }

  downloadUrl(`/api/mind-map-transfer/${selectedMap.value.id}/file`);
}

async function importStructure() {
  const raw = transferText.value.trim();
  if (!raw) {
    errorMessage.value = '请先粘贴导图结构体';
    return;
  }

  let parsed;
  try {
    parsed = JSON.parse(raw);
  } catch {
    errorMessage.value = '导图结构 JSON 格式无效';
    return;
  }

  const imported = await run(
    () => api('/api/mind-map-transfer/structure', {
      method: 'POST',
      body: JSON.stringify({
        mindMap: parsed.mindMap ?? parsed,
        titleOverride: importTitleOverride.value.trim() || null
      })
    }),
    '结构体已导入'
  );

  if (imported) {
    await loadMaps();
    await selectMap(imported.structure.map.id);
  }
}

async function importFile(event) {
  const file = event.target.files?.[0];
  if (!file) {
    return;
  }

  const form = new FormData();
  form.append('file', file);
  if (importTitleOverride.value.trim()) {
    form.append('titleOverride', importTitleOverride.value.trim());
  }

  const imported = await run(
    () => api('/api/mind-map-transfer/file', { method: 'POST', body: form }),
    '文件已导入'
  );

  if (fileInput.value) {
    fileInput.value.value = '';
  }

  if (imported) {
    await loadMaps();
    await selectMap(imported.structure.map.id);
  }
}

async function cleanNaturalLanguage() {
  const naturalLanguage = naturalLanguageInput.value.trim();
  if (!naturalLanguage) {
    errorMessage.value = '请输入自然语言内容';
    return;
  }

  transferText.value = '';
  aiStatus.value = 'AI 正在清洗文本...';
  const result = await run(
    () => api('/api/ai/clean', {
      method: 'POST',
      body: JSON.stringify({
        naturalLanguage,
        modelId: selectedAiModelId.value || null
      })
    }),
    'AI 结构体已生成'
  );

  if (result) {
    transferText.value = JSON.stringify(result.transfer, null, 2);
    aiStatus.value = 'AI 结构体已生成';
  } else {
    aiStatus.value = '';
  }
}

function openChat() {
  chatOpen.value = true;
}

function closeChat() {
  chatOpen.value = false;
}

function startNewConversation() {
  chatMessages.value = [];
  chatInput.value = '';
  chatConversationId.value = createConversationId();
  aiStatus.value = '已开始新对话';
}

function buildConversationContext(messages = chatMessages.value) {
  return messages
    .map((message) => `${message.role === 'user' ? '用户' : 'AI'}：${message.content}`)
    .join('\n\n');
}

async function sendChatMessage() {
  const message = chatInput.value.trim();
  if (!message) {
    errorMessage.value = '请输入对话内容';
    return;
  }

  const previousContext = buildConversationContext();
  chatMessages.value.push({ role: 'user', content: message });
  chatInput.value = '';
  aiStatus.value = 'AI 正在回复...';

  const result = await run(
    () => api('/api/ai/context-chat', {
      method: 'POST',
      body: JSON.stringify({
        message,
        conversationId: chatConversationId.value,
        context: previousContext,
        modelId: selectedAiModelId.value || null
      })
    }),
    'AI 已回复'
  );

  if (result) {
    chatMessages.value.push({ role: 'assistant', content: result.reply });
    aiStatus.value = result.wasContextCompressed
      ? 'AI 已回复，较长对话上下文已先压缩'
      : 'AI 已回复';
  } else {
    aiStatus.value = '';
  }
}

async function cleanConversationContext() {
  const context = chatContextText.value.trim();
  if (!context) {
    errorMessage.value = '请先开始一轮对话';
    return;
  }

  transferText.value = '';
  aiStatus.value = 'AI 正在根据本次对话生成结构体...';
  const result = await run(
    () => api('/api/ai/clean', {
      method: 'POST',
      body: JSON.stringify({
        naturalLanguage: context,
        modelId: selectedAiModelId.value || null
      })
    }),
    '本次对话已生成结构体'
  );

  if (result) {
    transferText.value = JSON.stringify(result.transfer, null, 2);
    aiStatus.value = '本次对话已生成结构体';
  } else {
    aiStatus.value = '';
  }
}

onMounted(async () => {
  await Promise.all([loadMaps(), loadAiModels()]);
});
</script>

<template>
  <main class="workspace">
    <header class="topbar">
      <div>
        <p class="eyebrow">P1.4</p>
        <h1>NetMind</h1>
      </div>
      <div class="topbar-actions">
        <a class="link-button" href="/swagger" target="_blank">接口文档</a>
        <button type="button" data-testid="refresh-maps" @click="loadMaps" :disabled="loading">刷新</button>
      </div>
    </header>

    <section v-if="errorMessage" class="message error" data-testid="error-message">{{ errorMessage }}</section>
    <section v-else-if="noticeMessage" class="message notice" data-testid="notice-message">{{ noticeMessage }}</section>

    <section class="layout">
      <aside class="sidebar">
        <div class="section-heading">
          <h2>导图</h2>
          <span>{{ maps.length }}</span>
        </div>
        <div class="field-row">
          <input v-model="mapTitle" data-testid="map-title" type="text" placeholder="导图标题" />
          <button type="button" data-testid="create-map" @click="createMap" :disabled="loading">新增</button>
        </div>
        <div class="map-list">
          <button
            v-for="map in maps"
            :key="map.id"
            type="button"
            class="map-item"
            :class="{ active: map.id === selectedMapId }"
            @click="selectMap(map.id)"
          >
            <span>{{ map.title }}</span>
            <small>#{{ map.id }}</small>
          </button>
        </div>
        <div class="button-grid">
          <button type="button" data-testid="rename-map" @click="renameMap" :disabled="loading || !selectedMap">
            重命名
          </button>
          <button type="button" @click="deleteMap(false)" :disabled="loading || !selectedMap">删除导图</button>
          <button type="button" class="danger" @click="deleteMap(true)" :disabled="loading || !selectedMap">
            删除全部
          </button>
        </div>
      </aside>

      <section class="canvas">
        <div class="section-heading">
          <h2>{{ selectedMap?.title ?? '未选择导图' }}</h2>
          <span>{{ nodes.length }} 个节点</span>
        </div>
        <div v-if="visualNodes.length === 0" class="empty">暂无节点，请先在右侧编辑区创建根节点。</div>
        <div v-else class="node-list" data-testid="node-list">
          <button
            v-for="node in visualNodes"
            :key="node.id"
            type="button"
            class="node-row"
            :class="{ active: node.id === selectedNodeId }"
            :style="{ '--depth': node.depth }"
            @click="selectNode(node.id)"
          >
            <span class="node-title">{{ node.title }}</span>
            <span class="node-meta">{{ node.childCount }} 个子节点</span>
          </button>
        </div>

        <section class="ai-panel">
          <div class="section-heading">
            <h2>AI 清洗</h2>
            <span>{{ aiModels.length }} 个模型</span>
          </div>
          <div class="field-row ai-actions">
            <select v-model="selectedAiModelId" data-testid="ai-model">
              <option v-for="model in aiModels" :key="model.id" :value="model.id">
                {{ model.name }}
              </option>
            </select>
            <button type="button" data-testid="ai-clean" @click="cleanNaturalLanguage" :disabled="loading">
              自然语言清洗
            </button>
            <button type="button" data-testid="ai-open-chat" @click="openChat" :disabled="loading">
              需求对话
            </button>
          </div>
          <p v-if="aiStatus" class="inline-status" data-testid="ai-status">{{ aiStatus }}</p>
          <textarea
            v-model="naturalLanguageInput"
            data-testid="ai-natural-language"
            rows="7"
            placeholder="请输入自然语言描述，AI 会扩充为标准导图结构 JSON。"
          ></textarea>
        </section>

        <section class="transfer-panel">
          <div class="section-heading">
            <h2>导入 / 导出</h2>
            <span>JSON</span>
          </div>
          <div class="transfer-actions">
            <button type="button" data-testid="export-structure" @click="exportStructure" :disabled="loading || !selectedMap">
              导出结构
            </button>
            <button type="button" data-testid="export-file" @click="downloadSelectedMap" :disabled="loading || !selectedMap">
              导出文件
            </button>
            <button type="button" data-testid="download-template" @click="downloadUrl('/api/mind-map-transfer/template')">
              模板
            </button>
            <button type="button" data-testid="import-structure" @click="importStructure" :disabled="loading">
              导入结构
            </button>
          </div>
          <div class="field-row transfer-import-row">
            <input v-model="importTitleOverride" data-testid="import-title" type="text" placeholder="可选：导入后的标题" />
            <input ref="fileInput" data-testid="import-file" type="file" accept="application/json,.json" @change="importFile" />
          </div>
          <textarea
            v-model="transferText"
            data-testid="transfer-text"
            rows="10"
            placeholder="导出的结构会显示在这里，也可以粘贴模板或导图 JSON 后导入。"
          ></textarea>
        </section>
      </section>

      <aside class="inspector">
        <div class="section-heading">
          <h2>节点</h2>
          <span>{{ selectedNode ? `#${selectedNode.id}` : '未选择' }}</span>
        </div>
        <label>
          标题
          <input v-model="nodeForm.title" data-testid="node-title" type="text" placeholder="节点标题" />
        </label>
        <label>
          内容
          <textarea v-model="nodeForm.content" data-testid="node-content" rows="5" placeholder="节点内容"></textarea>
        </label>
        <label>
          排序
          <input v-model="nodeForm.orderNo" data-testid="node-order" type="number" min="0" />
        </label>
        <div class="button-grid">
          <button type="button" data-testid="create-root-node" @click="createNode(null)" :disabled="loading || !selectedMap">
            根节点
          </button>
          <button
            type="button"
            data-testid="create-child-node"
            @click="createNode(selectedNode?.id ?? null)"
            :disabled="loading || !selectedMap"
          >
            子节点
          </button>
          <button type="button" data-testid="save-node" @click="updateNode" :disabled="loading || !selectedNode">
            保存
          </button>
          <button type="button" @click="deleteNode(false)" :disabled="loading || !selectedNode">删除节点</button>
          <button type="button" class="danger" @click="deleteNode(true)" :disabled="loading || !selectedNode">
            删除子树
          </button>
        </div>

        <div class="section-heading relation-title">
          <h2>关联</h2>
          <span>{{ selectedNodeRelations.length }}/{{ relations.length }}</span>
        </div>
        <label>
          目标节点
          <select v-model="relationForm.targetId" data-testid="relation-target" :disabled="!selectedNode">
            <option value="">请选择目标</option>
            <option v-for="node in candidateTargets" :key="node.id" :value="node.id">
              {{ node.title }}
            </option>
          </select>
        </label>
        <label>
          类型
          <input v-model="relationForm.relationType" data-testid="relation-type" type="text" />
        </label>
        <label>
          权重
          <input v-model="relationForm.weight" data-testid="relation-weight" type="number" min="0" step="0.1" />
        </label>
        <button type="button" data-testid="create-relation" @click="createRelation" :disabled="loading || !selectedNode">
          新增关联
        </button>
        <div class="relation-list">
          <div v-for="relation in selectedNodeRelations" :key="relation.id" class="relation-row">
            <span>
              {{ nodeTitleById.get(relation.sourceId) ?? `#${relation.sourceId}` }}
              ->
              {{ nodeTitleById.get(relation.targetId) ?? `#${relation.targetId}` }}
              · {{ relation.relationType }}
            </span>
            <button type="button" @click="deleteRelation(relation.id)" :disabled="loading">删除</button>
          </div>
          <div v-if="selectedNode && selectedNodeRelations.length === 0" class="empty small">当前节点暂无关联。</div>
        </div>
      </aside>
    </section>

    <section v-if="chatOpen" class="modal-backdrop" data-testid="ai-chat-modal">
      <div class="chat-modal">
        <div class="section-heading">
          <h2>需求对话</h2>
          <span>{{ chatMessages.length }} 条消息</span>
        </div>
        <div class="chat-log">
          <div v-if="chatMessages.length === 0" class="empty small">本次对话还没有消息。</div>
          <div
            v-for="(message, index) in chatMessages"
            :key="index"
            class="chat-message"
            :class="message.role"
          >
            <strong>{{ message.role === 'user' ? '你' : 'AI' }}</strong>
            <p>{{ message.content }}</p>
          </div>
        </div>
        <textarea
          v-model="chatInput"
          data-testid="ai-chat-input"
          rows="4"
          placeholder="围绕需求继续对话，本次对话记录会作为程序管理的上下文。"
        ></textarea>
        <div class="chat-actions">
          <button type="button" data-testid="ai-chat-send" @click="sendChatMessage" :disabled="loading">发送</button>
          <button type="button" data-testid="ai-new-chat" @click="startNewConversation" :disabled="loading">新对话</button>
          <button type="button" data-testid="ai-chat-clean" @click="cleanConversationContext" :disabled="loading || chatMessages.length === 0">
            生成结构体
          </button>
          <button type="button" @click="closeChat" :disabled="loading">关闭</button>
        </div>
      </div>
    </section>
  </main>
</template>
