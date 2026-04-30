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
    throw new Error(text || `Request failed: ${response.status}`);
  }

  if (!response.ok || !result.success) {
    throw new Error(result.message || `Request failed: ${response.status}`);
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
    errorMessage.value = error instanceof Error ? error.message : 'Operation failed';
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
  const data = await run(() => api('/api/mind-maps'), 'Maps refreshed');
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
  await refreshMapData(id, { keepNodeId: null, message: 'Map loaded' });
}

async function createMap() {
  const title = mapTitle.value.trim();
  if (!title) {
    errorMessage.value = 'Map title is required';
    return;
  }

  const created = await run(
    () => api('/api/mind-maps', { method: 'POST', body: JSON.stringify({ title }) }),
    'Map created'
  );

  if (created) {
    await loadMaps();
    await selectMap(created.id);
  }
}

async function renameMap() {
  if (!selectedMap.value) {
    errorMessage.value = 'Select a map first';
    return;
  }

  const title = mapTitle.value.trim();
  if (!title) {
    errorMessage.value = 'New map title is required';
    return;
  }

  const updated = await run(
    () => api(`/api/mind-maps/${selectedMap.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({ title, rootNodeId: selectedMap.value.rootNodeId })
    }),
    'Map renamed'
  );

  if (updated) {
    await loadMaps();
  }
}

async function deleteMap(cascade) {
  if (!selectedMap.value) {
    errorMessage.value = 'Select a map first';
    return;
  }

  const deletedId = selectedMap.value.id;
  const deleted = await run(
    () => api(`/api/mind-maps/${deletedId}${cascade ? '/cascade' : ''}`, { method: 'DELETE' }),
    cascade ? 'Map and content deleted' : 'Map deleted'
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
    errorMessage.value = 'Select a map first';
    return;
  }

  const title = nodeForm.value.title.trim();
  if (!title) {
    errorMessage.value = 'Node title is required';
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
    'Node created'
  );

  if (created) {
    await refreshMapData(selectedMap.value.id, { keepNodeId: created.id });
  }
}

async function updateNode() {
  if (!selectedNode.value) {
    errorMessage.value = 'Select a node first';
    return;
  }

  const title = nodeForm.value.title.trim();
  if (!title) {
    errorMessage.value = 'Node title is required';
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
    'Node saved'
  );

  if (updated) {
    await refreshMapData(selectedMap.value.id, { keepNodeId: updated.id });
  }
}

async function deleteNode(subtree) {
  if (!selectedNode.value) {
    errorMessage.value = 'Select a node first';
    return;
  }

  const deletedNodeId = selectedNode.value.id;
  const deleted = await run(
    () => api(`/api/nodes/${deletedNodeId}${subtree ? '/subtree' : ''}`, { method: 'DELETE' }),
    subtree ? 'Node subtree deleted' : 'Node deleted'
  );

  if (deleted) {
    await refreshMapData(selectedMap.value.id, { keepNodeId: null });
  }
}

async function createRelation() {
  if (!selectedMap.value || !selectedNode.value) {
    errorMessage.value = 'Select a map and source node first';
    return;
  }

  const targetId = Number(relationForm.value.targetId);
  if (!targetId) {
    errorMessage.value = 'Target node is required';
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
    'Relation created'
  );

  if (created) {
    relationForm.value = { targetId: '', relationType: 'relates_to', weight: 1 };
    await refreshMapData(selectedMap.value.id, { keepNodeId: selectedNodeId.value });
  }
}

async function deleteRelation(id) {
  const deleted = await run(() => api(`/api/node-relations/${id}`, { method: 'DELETE' }), 'Relation deleted');
  if (deleted) {
    await refreshMapData(selectedMap.value.id, { keepNodeId: selectedNodeId.value });
  }
}

async function exportStructure() {
  if (!selectedMap.value) {
    errorMessage.value = 'Select a map first';
    return;
  }

  const structure = await run(
    () => api(`/api/mind-map-transfer/${selectedMap.value.id}/structure`),
    'Structure exported'
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
    errorMessage.value = 'Select a map first';
    return;
  }

  downloadUrl(`/api/mind-map-transfer/${selectedMap.value.id}/file`);
}

async function importStructure() {
  const raw = transferText.value.trim();
  if (!raw) {
    errorMessage.value = 'Paste a transfer structure first';
    return;
  }

  let parsed;
  try {
    parsed = JSON.parse(raw);
  } catch {
    errorMessage.value = 'Transfer JSON is invalid';
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
    'Structure imported'
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
    'File imported'
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
    errorMessage.value = 'Natural language input is required';
    return;
  }

  transferText.value = '';
  aiStatus.value = 'AI is cleaning the text...';
  const result = await run(
    () => api('/api/ai/clean', {
      method: 'POST',
      body: JSON.stringify({
        naturalLanguage,
        modelId: selectedAiModelId.value || null
      })
    }),
    'AI structure cleaned'
  );

  if (result) {
    transferText.value = JSON.stringify(result.transfer, null, 2);
    aiStatus.value = 'AI structure cleaned';
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
  aiStatus.value = '已开始新对话';
}

function buildConversationContext(messages = chatMessages.value) {
  return messages
    .map((message) => `${message.role === 'user' ? 'User' : 'AI'}: ${message.content}`)
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
        <p class="eyebrow">P1.3</p>
        <h1>NetMind</h1>
      </div>
      <div class="topbar-actions">
        <a class="link-button" href="/swagger" target="_blank">Swagger</a>
        <button type="button" data-testid="refresh-maps" @click="loadMaps" :disabled="loading">Refresh</button>
      </div>
    </header>

    <section v-if="errorMessage" class="message error" data-testid="error-message">{{ errorMessage }}</section>
    <section v-else-if="noticeMessage" class="message notice" data-testid="notice-message">{{ noticeMessage }}</section>

    <section class="layout">
      <aside class="sidebar">
        <div class="section-heading">
          <h2>Maps</h2>
          <span>{{ maps.length }}</span>
        </div>
        <div class="field-row">
          <input v-model="mapTitle" data-testid="map-title" type="text" placeholder="Map title" />
          <button type="button" data-testid="create-map" @click="createMap" :disabled="loading">Add</button>
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
            Rename
          </button>
          <button type="button" @click="deleteMap(false)" :disabled="loading || !selectedMap">Delete map</button>
          <button type="button" class="danger" @click="deleteMap(true)" :disabled="loading || !selectedMap">
            Delete all
          </button>
        </div>
      </aside>

      <section class="canvas">
        <div class="section-heading">
          <h2>{{ selectedMap?.title ?? 'No map selected' }}</h2>
          <span>{{ nodes.length }} nodes</span>
        </div>
        <div v-if="visualNodes.length === 0" class="empty">No nodes yet. Create a root node from the editor.</div>
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
            <span class="node-meta">{{ node.childCount }} child</span>
          </button>
        </div>

        <section class="ai-panel">
          <div class="section-heading">
            <h2>AI Clean</h2>
            <span>{{ aiModels.length }} model</span>
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
            placeholder="Describe a mind map in natural language. The cleaner will expand it into the standard transfer JSON."
          ></textarea>
        </section>

        <section class="transfer-panel">
          <div class="section-heading">
            <h2>Import / Export</h2>
            <span>JSON</span>
          </div>
          <div class="transfer-actions">
            <button type="button" data-testid="export-structure" @click="exportStructure" :disabled="loading || !selectedMap">
              Export structure
            </button>
            <button type="button" data-testid="export-file" @click="downloadSelectedMap" :disabled="loading || !selectedMap">
              Export file
            </button>
            <button type="button" data-testid="download-template" @click="downloadUrl('/api/mind-map-transfer/template')">
              Template
            </button>
            <button type="button" data-testid="import-structure" @click="importStructure" :disabled="loading">
              Import structure
            </button>
          </div>
          <div class="field-row transfer-import-row">
            <input v-model="importTitleOverride" data-testid="import-title" type="text" placeholder="Optional imported title" />
            <input ref="fileInput" data-testid="import-file" type="file" accept="application/json,.json" @change="importFile" />
          </div>
          <textarea
            v-model="transferText"
            data-testid="transfer-text"
            rows="10"
            placeholder="Exported structure appears here. You can also paste a template or transfer JSON and import it."
          ></textarea>
        </section>
      </section>

      <aside class="inspector">
        <div class="section-heading">
          <h2>Node</h2>
          <span>{{ selectedNode ? `#${selectedNode.id}` : 'None' }}</span>
        </div>
        <label>
          Title
          <input v-model="nodeForm.title" data-testid="node-title" type="text" placeholder="Node title" />
        </label>
        <label>
          Content
          <textarea v-model="nodeForm.content" data-testid="node-content" rows="5" placeholder="Node content"></textarea>
        </label>
        <label>
          Order
          <input v-model="nodeForm.orderNo" data-testid="node-order" type="number" min="0" />
        </label>
        <div class="button-grid">
          <button type="button" data-testid="create-root-node" @click="createNode(null)" :disabled="loading || !selectedMap">
            Root
          </button>
          <button
            type="button"
            data-testid="create-child-node"
            @click="createNode(selectedNode?.id ?? null)"
            :disabled="loading || !selectedMap"
          >
            Child
          </button>
          <button type="button" data-testid="save-node" @click="updateNode" :disabled="loading || !selectedNode">
            Save
          </button>
          <button type="button" @click="deleteNode(false)" :disabled="loading || !selectedNode">Delete node</button>
          <button type="button" class="danger" @click="deleteNode(true)" :disabled="loading || !selectedNode">
            Delete tree
          </button>
        </div>

        <div class="section-heading relation-title">
          <h2>Relation</h2>
          <span>{{ selectedNodeRelations.length }}/{{ relations.length }}</span>
        </div>
        <label>
          Target
          <select v-model="relationForm.targetId" data-testid="relation-target" :disabled="!selectedNode">
            <option value="">Choose target</option>
            <option v-for="node in candidateTargets" :key="node.id" :value="node.id">
              {{ node.title }}
            </option>
          </select>
        </label>
        <label>
          Type
          <input v-model="relationForm.relationType" data-testid="relation-type" type="text" />
        </label>
        <label>
          Weight
          <input v-model="relationForm.weight" data-testid="relation-weight" type="number" min="0" step="0.1" />
        </label>
        <button type="button" data-testid="create-relation" @click="createRelation" :disabled="loading || !selectedNode">
          Add relation
        </button>
        <div class="relation-list">
          <div v-for="relation in selectedNodeRelations" :key="relation.id" class="relation-row">
            <span>
              {{ nodeTitleById.get(relation.sourceId) ?? `#${relation.sourceId}` }}
              ->
              {{ nodeTitleById.get(relation.targetId) ?? `#${relation.targetId}` }}
              · {{ relation.relationType }}
            </span>
            <button type="button" @click="deleteRelation(relation.id)" :disabled="loading">Delete</button>
          </div>
          <div v-if="selectedNode && selectedNodeRelations.length === 0" class="empty small">No relation for this node.</div>
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
