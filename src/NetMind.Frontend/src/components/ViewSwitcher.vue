<script setup>
defineProps({
  workMode: { type: String, required: true },
  viewMode: { type: String, required: true }
});

defineEmits(['update:workMode', 'update:viewMode']);
</script>

<template>
  <div class="switcher-bar">
    <el-segmented
      :model-value="workMode"
      :options="[
        { label: '展示', value: 'display' },
        { label: '工作台', value: 'workbench' }
      ]"
      @update:model-value="$emit('update:workMode', $event)"
    />
    <el-segmented
      :model-value="viewMode"
      :options="[
        { label: '图', value: 'graph' },
        { label: '列表', value: 'list' }
      ]"
      @update:model-value="$emit('update:viewMode', $event)"
    />
  </div>
</template>
