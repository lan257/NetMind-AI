<script setup>
defineProps({
  modelValue: { type: Boolean, required: true },
  node: { type: Object, default: null }
});

defineEmits(['update:modelValue']);
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="node?.title ?? '节点内容'"
    width="720px"
    class="node-preview-dialog"
    :close-on-click-modal="false"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <div class="node-preview">
      <p v-if="node?.content">{{ node.content }}</p>
      <p v-else class="muted">该节点暂无内容。</p>
      <div class="node-preview-meta">
        <span>节点编号：{{ node?.id ?? '-' }}</span>
        <span>排序：{{ node?.orderNo ?? '-' }}</span>
      </div>
    </div>
  </el-dialog>
</template>
