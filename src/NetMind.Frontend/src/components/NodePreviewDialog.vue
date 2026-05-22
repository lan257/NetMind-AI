<script setup>
import { ref, watch } from 'vue';
import { FullScreen, Refresh, Location } from '@element-plus/icons-vue';
import { renderMarkdown } from '../composables/useMarkdown';
import { api } from '../services/api';
import RelationGraphCanvas from './RelationGraphCanvas.vue';

const props = defineProps({
  modelValue: { type: Boolean, required: true },
  node: { type: Object, default: null },
  nodes: { type: Array, default: () => [] },
  relations: { type: Array, default: () => [] },
  currentMapId: { type: [Number, String], default: null }
});

const emit = defineEmits(['update:modelValue', 'preview-node', 'jump-to-node']);

const graphOpen = ref(false);
const history = ref([]);
const currentNode = ref(null);
const currentRelations = ref([]);
const loading = ref(false);

function mergeRelations(mapRelations, nodeRelations) {
  const relationById = new Map();
  [...mapRelations, ...nodeRelations].forEach((relation) => {
    relationById.set(relation.id ?? `${relation.sourceId}-${relation.targetId}-${relation.relationType}`, relation);
  });
  return [...relationById.values()];
}

// 当外部 node 变化时，重置历史并加载初始数据
watch(() => props.node, (val) => {
  if (val) {
    history.value = [val];
    loadNodeData(val);
  }
}, { immediate: true });

async function loadNodeData(node) {
  currentNode.value = node;
  currentRelations.value = props.relations; // 默认使用传入的关联
  
  if (!node) return;

  loading.value = true;
  try {
    // 同时获取节点详情（包含内容）和所有关联（跨图）
    const [nodeResult, relationResult] = await Promise.all([
      api(`/api/nodes/${node.id}`),
      api(`/api/node-relations/by-node/${node.id}`)
    ]);
    
    if (nodeResult) {
      currentNode.value = nodeResult;
      // 更新历史记录中的当前项
      const idx = history.value.findIndex(h => h.id === node.id);
      if (idx !== -1) history.value[idx] = nodeResult;
    }
    
    if (relationResult) {
      currentRelations.value = mergeRelations(props.relations, relationResult);
    }
  } catch (err) {
    console.error('Failed to load node preview data:', err);
  } finally {
    loading.value = false;
  }
}

async function navigateTo(node) {
  if (node.id === currentNode.value?.id) return;
  history.value.push(node);
  await loadNodeData(node);
}

async function goBack() {
  if (history.value.length > 1) {
    history.value.pop();
    const prevNode = history.value[history.value.length - 1];
    await loadNodeData(prevNode);
  }
}

function jumpToMap() {
  if (currentNode.value) {
    emit('jump-to-node', {
      mapId: currentNode.value.mapId,
      nodeId: currentNode.value.id
    });
    emit('update:modelValue', false);
  }
}

function handleContentClick(event) {
  const target = event.target;
  if (target.tagName === 'A' && target.classList.contains('node-ref')) {
    event.preventDefault();
    const id = Number(target.getAttribute('data-id'));
    
    // 先在 props.nodes 中找
    let targetNode = props.nodes.find(n => n.id === id);
    
    // 如果没找到（可能是跨图），构造一个基础节点，loadNodeData 会负责补充详情
    if (!targetNode) {
      const rel = currentRelations.value.find(r => r.sourceId === id || r.targetId === id);
      targetNode = {
        id: id,
        title: rel ? (rel.sourceId === id ? rel.sourceTitle : rel.targetTitle) : `节点 #${id}`,
        isExternal: true
      };
    }

    if (targetNode) {
      navigateTo(targetNode);
    }
  }
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="currentNode?.title ?? '节点内容'"
    width="min(720px, calc(100vw - 32px))"
    class="node-preview-dialog"
    :close-on-click-modal="false"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <template #header>
      <div class="dialog-header-stack">
        <el-button v-if="history.length > 1" link :icon="Refresh" @click="goBack">返回</el-button>
        <span class="el-dialog__title">{{ currentNode?.title ?? '节点内容' }}</span>
        <el-tag v-if="currentNode?.isExternal || (currentNode?.mapId && String(currentNode.mapId) !== String(props.currentMapId))" type="info" size="small" effect="plain" style="margin-left: 8px">跨图</el-tag>
        <el-button
          v-if="currentNode && currentNode.mapId && String(currentNode.mapId) !== String(props.currentMapId)"
          link
          type="primary"
          :icon="Location"
          style="margin-left: auto; margin-right: 16px;"
          @click="jumpToMap"
        >
          跳转至导图
        </el-button>
      </div>
    </template>
    
    <div class="node-preview" v-loading="loading">
      <div v-if="currentNode?.content" class="markdown-body" @click="handleContentClick" v-html="renderMarkdown(currentNode.content)"></div>
      <p v-else-if="!loading" class="muted">该节点暂无内容。</p>
      
      <div class="node-preview-meta">
        <span>节点编号：{{ currentNode?.id ?? '-' }}</span>
        <span v-if="currentNode?.mapTitle">所属导图：{{ currentNode.mapTitle }}</span>
        <span v-if="!currentNode?.isExternal">排序：{{ currentNode?.orderNo ?? '-' }}</span>
      </div>

      <section class="relation-preview">
        <div class="section-heading">
          <h2>关联图谱</h2>
          <el-button :icon="FullScreen" :disabled="!currentNode" @click="graphOpen = true">详情大图</el-button>
        </div>
        <RelationGraphCanvas
          :center-node="currentNode"
          :nodes="nodes"
          :relations="currentRelations"
          :height="240"
          :show-labels="false"
          :node-draggable="false"
          @preview-node="navigateTo"
        />
      </section>
    </div>
  </el-dialog>

  <el-dialog v-model="graphOpen" title="关联图谱" width="min(1080px, calc(100vw - 32px))" class="relation-graph-dialog">
    <RelationGraphCanvas
      :center-node="currentNode"
      :nodes="nodes"
      :relations="currentRelations"
      :height="620"
      :node-draggable="false"
      @preview-node="navigateTo"
    />
  </el-dialog>
</template>

<style scoped>
.dialog-header-stack {
  display: flex;
  align-items: center;
  gap: 12px;
}
</style>
