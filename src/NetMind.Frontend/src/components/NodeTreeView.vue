<script setup>
import { computed, ref, watch } from 'vue';
import { ArrowDown, ArrowRight } from '@element-plus/icons-vue';

const props = defineProps({
  nodes: { type: Array, default: () => [] },
  map: { type: Object, default: null },
  selectedNodeId: { type: [Number, String, null], default: null },
  previewOnClick: { type: Boolean, default: true }
});

const emit = defineEmits(['select-node', 'preview-node']);
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
      <span>{{ nodes.length }} 个节点</span>
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
