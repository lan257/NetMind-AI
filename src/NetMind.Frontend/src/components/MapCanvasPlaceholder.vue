<script setup>
defineProps({
  map: { type: Object, default: null },
  nodes: { type: Array, default: () => [] }
});
</script>

<template>
  <section class="canvas-panel" data-testid="graph-placeholder">
    <div class="section-heading">
      <h2>{{ map?.title ?? '未选择导图' }}</h2>
      <span>{{ nodes.length }} 个节点</span>
    </div>
    <div class="graph-placeholder">
      <div class="graph-root">{{ map?.title ?? '思维导图画布' }}</div>
      <div class="graph-spokes">
        <span v-for="node in nodes.slice(0, 6)" :key="node.id">{{ node.title }}</span>
      </div>
    </div>
  </section>
</template>
