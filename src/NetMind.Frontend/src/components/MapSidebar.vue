<script setup>
import { Plus } from '@element-plus/icons-vue';

defineProps({
  maps: { type: Array, default: () => [] },
  selectedMapId: { type: [Number, String, null], default: null },
  loading: { type: Boolean, default: false }
});

defineEmits(['select-map', 'create-map']);
</script>

<template>
  <aside class="sidebar">
    <div class="section-heading">
      <h2>思维导图</h2>
      <span>{{ maps.length }} 个</span>
    </div>
    <el-button class="wide-action" type="primary" :icon="Plus" data-testid="open-create-map" @click="$emit('create-map')">
      新增思维导图
    </el-button>
    <div class="map-list">
      <button
        v-for="map in maps"
        :key="map.id"
        type="button"
        class="map-item"
        :class="{ active: map.id === selectedMapId }"
        :disabled="loading"
        @click="$emit('select-map', map.id)"
      >
        <span>{{ map.title }}</span>
        <small>#{{ map.id }}</small>
      </button>
      <div v-if="maps.length === 0" class="empty small">暂无思维导图。</div>
    </div>
  </aside>
</template>
