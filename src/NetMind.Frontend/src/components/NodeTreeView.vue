<script setup>
import { computed, ref, watch } from 'vue';
import { ArrowDown, ArrowRight, Plus, Delete, Refresh } from '@element-plus/icons-vue';

const props = defineProps({
  nodes: { type: Array, default: () => [] },
  map: { type: Object, default: null },
  selectedNodeId: { type: [Number, String, null], default: null },
  previewOnClick: { type: Boolean, default: true },
  editable: { type: Boolean, default: false },
  loading: { type: Boolean, default: false },
  selectedNode: { type: Object, default: null }
});

const emit = defineEmits(['select-node', 'preview-node', 'create-root', 'create-child', 'delete-node', 'refresh-nodes']);
const collapsedIds = ref(new Set());

const nodeRows = computed(() => {
  const byParent = new Map();
  props.nodes.forEach((node) => {
    const key = node.parentId ?? 0;
    if (!byParent.has(key)) {
      byParent.set(key, []);
    }
    byParent.get(key).push(node);
  });

  byParent.forEach((items) => {
    items.sort((left, right) => left.orderNo - right.orderNo || left.id - right.id);
  });

  const rows = [];
  const walk = (parentId, depth) => {
    (byParent.get(parentId) ?? []).forEach((node) => {
      const childCount = byParent.get(node.id)?.length ?? 0;
      rows.push({ ...node, depth, childCount, collapsed: collapsedIds.value.has(node.id) });
      if (!collapsedIds.value.has(node.id)) {
        walk(node.id, depth + 1);
      }
    });
  };
  walk(0, 0);
  return rows;
});

watch(
  () => props.nodes,
  () => {
    collapsedIds.value = new Set([...collapsedIds.value].filter((id) => props.nodes.some((node) => node.id === id)));
  }
);

function toggle(node) {
  const next = new Set(collapsedIds.value);
  if (next.has(node.id)) {
    next.delete(node.id);
  } else {
    next.add(node.id);
  }
  collapsedIds.value = next;
}

function openNode(node) {
  emit('select-node', node.id);
  if (props.previewOnClick) {
    emit('preview-node', node);
  }
}
</script>

<template>
  <section class="canvas-panel">
    <div class="section-heading">
      <h2>{{ map?.title ?? '未选择导图' }}</h2>
      <div class="heading-actions">
        <span>{{ nodes.length }} 个节点</span>
        <el-button
          size="small"
          :icon="Refresh"
          :disabled="loading || !map"
          data-testid="refresh-node-list"
          @click="$emit('refresh-nodes')"
        >
          刷新
        </el-button>
      </div>
    </div>
    <div v-if="editable" style="display:flex;justify-content:flex-end;gap:6px;margin-bottom:10px;">
      <el-button size="small" type="primary" :icon="Plus" :disabled="loading || !map" @click="$emit('create-root')">根节点</el-button>
      <el-button size="small" :icon="Plus" :disabled="loading || !map || !selectedNode" @click="$emit('create-child')">子节点</el-button>
      <el-button size="small" type="danger" :icon="Delete" :disabled="loading || !selectedNode" @click="$emit('delete-node')">删除</el-button>
    </div>
    <div v-if="nodeRows.length === 0" class="empty">暂无节点。</div>
    <div v-else class="node-list" data-testid="node-list">
      <div
        v-for="node in nodeRows"
        :key="node.id"
        class="node-row-wrap"
        :class="{ active: node.id === selectedNodeId }"
        :style="{ '--depth': node.depth }"
      >
        <button
          type="button"
          class="collapse-button"
          :disabled="node.childCount === 0"
          @click.stop="toggle(node)"
        >
          <el-icon v-if="node.childCount > 0">
            <ArrowRight v-if="node.collapsed" />
            <ArrowDown v-else />
          </el-icon>
        </button>
        <button type="button" class="node-content-button" @click="openNode(node)">
          <span class="node-title">{{ node.title }}</span>
          <span class="node-meta">{{ node.childCount }} 个子节点</span>
        </button>
      </div>
    </div>
  </section>
</template>

<style scoped>
.heading-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

.node-list {
  max-height: calc(100vh - 260px);
  overflow-y: auto;
}
</style>
