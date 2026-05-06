<script setup>
import { ref } from 'vue';
import { FullScreen } from '@element-plus/icons-vue';
import { renderMarkdown } from '../composables/useMarkdown';
import RelationGraphCanvas from './RelationGraphCanvas.vue';

defineProps({
  modelValue: { type: Boolean, required: true },
  node: { type: Object, default: null },
  nodes: { type: Array, default: () => [] },
  relations: { type: Array, default: () => [] }
});

defineEmits(['update:modelValue', 'preview-node']);

const graphOpen = ref(false);
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="node?.title ?? '节点内容'"
    width="min(720px, calc(100vw - 32px))"
    class="node-preview-dialog"
    :close-on-click-modal="false"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <div class="node-preview">
      <div v-if="node?.content" class="markdown-body" v-html="renderMarkdown(node.content)"></div>
      <p v-else class="muted">该节点暂无内容。</p>
      <div class="node-preview-meta">
        <span>节点编号：{{ node?.id ?? '-' }}</span>
        <span>排序：{{ node?.orderNo ?? '-' }}</span>
      </div>
      <section class="relation-preview">
        <div class="section-heading">
          <h2>关联图谱</h2>
          <el-button :icon="FullScreen" :disabled="!node" @click="graphOpen = true">放大</el-button>
        </div>
        <RelationGraphCanvas
          :center-node="node"
          :nodes="nodes"
          :relations="relations"
          :height="240"
          :node-draggable="false"
          @preview-node="$emit('preview-node', $event)"
        />
      </section>
    </div>
  </el-dialog>

  <el-dialog v-model="graphOpen" title="关联图谱" width="min(1080px, calc(100vw - 32px))" class="relation-graph-dialog">
    <RelationGraphCanvas
      :center-node="node"
      :nodes="nodes"
      :relations="relations"
      :height="620"
      :node-draggable="false"
      @preview-node="$emit('preview-node', $event)"
    />
  </el-dialog>
</template>
