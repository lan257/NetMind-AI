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
const loading = ref(false);
const errorMessage = ref('');
const noticeMessage = ref('');

const selectedMap = computed(() => maps.value.find((map) => map.id === selectedMapId.value) ?? null);
const selectedNode = computed(() => nodes.value.find((node) => node.id === selectedNodeId.value) ?? null);
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

async function api(path, options = {}) {
  const response = await fetch(path, {
    headers: { 'Content-Type': 'application/json', ...(options.headers ?? {}) },
    ...options
  });
  const result = await response.json();
  if (!response.ok || !result.success) {
    throw new Error(result.message || `请求失败：${response.status}`);
  }
  return result.data;
}

async function run(action, successMessage) {
  loading.value = true;
  errorMessage.value = '';
  noticeMessage.value = '';
  try {
    const result = await action();
    noticeMessage.value = successMessage;
    return result;
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '操作失败';
    return null;
  } finally {
    loading.value = false;
  }
}

async function loadMaps() {
  const data = await run(() => api('/api/mind-maps'), '导图已刷新');
  if (!data) {
    return;
  }
  maps.value = data;
  if (!selectedMapId.value && maps.value.length > 0) {
    await selectMap(maps.value[0].id);
  }
}

async function selectMap(id) {
  selectedMapId.value = id;
  selectedNodeId.value = null;
  const [nodeData, relationData] = await Promise.all([
    run(() => api(`/api/nodes/by-map/${id}`), '节点已加载'),
    run(() => api(`/api/node-relations/by-map/${id}`), '关联已加载')
  ]);
  nodes.value = nodeData ?? [];
  relations.value = relationData ?? [];
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
    mapTitle.value = '';
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
  await run(
    () => api(`/api/mind-maps/${selectedMap.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({ title, rootNodeId: selectedMap.value.rootNodeId })
    }),
    '导图已重命名'
  );
  mapTitle.value = '';
  await loadMaps();
}

async function deleteMap(cascade) {
  if (!selectedMap.value) {
    errorMessage.value = '请先选择导图';
    return;
  }
  await run(
    () => api(`/api/mind-maps/${selectedMap.value.id}${cascade ? '/cascade' : ''}`, { method: 'DELETE' }),
    cascade ? '导图及相关节点已逻辑删除' : '导图已逻辑删除'
  );
  selectedMapId.value = null;
  nodes.value = [];
  relations.value = [];
  await loadMaps();
}

function selectNode(id) {
  selectedNodeId.value = id;
  const node = selectedNode.value;
  if (node) {
    nodeForm.value = {
      title: node.title,
      content: node.content ?? '',
      orderNo: node.orderNo
    };
  }
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
    nodeForm.value = { title: '', content: '', orderNo: 1 };
    await selectMap(selectedMap.value.id);
    selectNode(created.id);
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
  await run(
    () => api(`/api/nodes/${selectedNode.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        parentId: selectedNode.value.parentId,
        title,
        content: nodeForm.value.content,
        orderNo: Number(nodeForm.value.orderNo) || 0
      })
    }),
    '节点已更新'
  );
  await selectMap(selectedMap.value.id);
}

async function deleteNode(subtree) {
  if (!selectedNode.value) {
    errorMessage.value = '请先选择节点';
    return;
  }
  const deletedNodeId = selectedNode.value.id;
  await run(
    () => api(`/api/nodes/${deletedNodeId}${subtree ? '/subtree' : ''}`, { method: 'DELETE' }),
    subtree ? '节点及子树已逻辑删除' : '节点本身已逻辑删除'
  );
  await selectMap(selectedMap.value.id);
}

async function createRelation() {
  if (!selectedMap.value || !selectedNode.value) {
    errorMessage.value = '请先选择导图和起点节点';
    return;
  }
  const targetId = Number(relationForm.value.targetId);
  if (!targetId) {
    errorMessage.value = '请选择目标节点';
    return;
  }
  await run(
    () => api('/api/node-relations', {
      method: 'POST',
      body: JSON.stringify({
        mapId: selectedMap.value.id,
        sourceId: selectedNode.value.id,
        targetId,
        relationType: relationForm.value.relationType,
        weight: Number(relationForm.value.weight) || 0
      })
    }),
    '节点关联已创建'
  );
  relationForm.value = { targetId: '', relationType: 'relates_to', weight: 1 };
  await selectMap(selectedMap.value.id);
}

async function deleteRelation(id) {
  await run(() => api(`/api/node-relations/${id}`, { method: 'DELETE' }), '节点关联已逻辑删除');
  await selectMap(selectedMap.value.id);
}

onMounted(loadMaps);
</script>

<template>
  <main class="workspace">
    <header class="topbar">
      <div>
        <p class="eyebrow">P0.4</p>
        <h1>NetMind</h1>
      </div>
      <div class="topbar-actions">
        <a class="link-button" href="/swagger" target="_blank">Swagger</a>
        <button type="button" @click="loadMaps" :disabled="loading">刷新</button>
      </div>
    </header>

    <section v-if="errorMessage" class="message error">{{ errorMessage }}</section>
    <section v-else-if="noticeMessage" class="message notice">{{ noticeMessage }}</section>

    <section class="layout">
      <aside class="sidebar">
        <div class="section-heading">
          <h2>导图</h2>
          <span>{{ maps.length }}</span>
        </div>
        <div class="field-row">
          <input v-model="mapTitle" type="text" placeholder="导图标题" />
          <button type="button" @click="createMap" :disabled="loading">新增</button>
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
          <button type="button" @click="renameMap" :disabled="loading || !selectedMap">重命名</button>
          <button type="button" @click="deleteMap(false)" :disabled="loading || !selectedMap">删导图</button>
          <button type="button" class="danger" @click="deleteMap(true)" :disabled="loading || !selectedMap">删导图+内容</button>
        </div>
      </aside>

      <section class="canvas">
        <div class="section-heading">
          <h2>{{ selectedMap?.title ?? '未选择导图' }}</h2>
          <span>{{ nodes.length }} 节点</span>
        </div>
        <div v-if="visualNodes.length === 0" class="empty">暂无节点，先在右侧创建根节点。</div>
        <div v-else class="node-list">
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
            <span class="node-meta">{{ node.childCount }} 子节点</span>
          </button>
        </div>
      </section>

      <aside class="inspector">
        <div class="section-heading">
          <h2>节点</h2>
          <span>{{ selectedNode ? `#${selectedNode.id}` : '未选择' }}</span>
        </div>
        <label>
          标题
          <input v-model="nodeForm.title" type="text" placeholder="节点标题" />
        </label>
        <label>
          内容
          <textarea v-model="nodeForm.content" rows="5" placeholder="节点内容"></textarea>
        </label>
        <label>
          排序
          <input v-model="nodeForm.orderNo" type="number" min="0" />
        </label>
        <div class="button-grid">
          <button type="button" @click="createNode(null)" :disabled="loading || !selectedMap">建根节点</button>
          <button type="button" @click="createNode(selectedNode?.id ?? null)" :disabled="loading || !selectedMap">建子节点</button>
          <button type="button" @click="updateNode" :disabled="loading || !selectedNode">保存节点</button>
          <button type="button" @click="deleteNode(false)" :disabled="loading || !selectedNode">删本身</button>
          <button type="button" class="danger" @click="deleteNode(true)" :disabled="loading || !selectedNode">删子树</button>
        </div>

        <div class="section-heading relation-title">
          <h2>关联</h2>
          <span>{{ relations.length }}</span>
        </div>
        <label>
          目标节点
          <select v-model="relationForm.targetId" :disabled="!selectedNode">
            <option value="">请选择</option>
            <option v-for="node in nodes" :key="node.id" :value="node.id" :disabled="node.id === selectedNodeId">
              {{ node.title }}
            </option>
          </select>
        </label>
        <label>
          类型
          <input v-model="relationForm.relationType" type="text" />
        </label>
        <label>
          权重
          <input v-model="relationForm.weight" type="number" min="0" step="0.1" />
        </label>
        <button type="button" @click="createRelation" :disabled="loading || !selectedNode">新增关联</button>
        <div class="relation-list">
          <div v-for="relation in relations" :key="relation.id" class="relation-row">
            <span>#{{ relation.sourceId }} → #{{ relation.targetId }} · {{ relation.relationType }}</span>
            <button type="button" @click="deleteRelation(relation.id)" :disabled="loading">删除</button>
          </div>
        </div>
      </aside>
    </section>
  </main>
</template>
