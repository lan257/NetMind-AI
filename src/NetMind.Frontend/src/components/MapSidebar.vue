<script setup>
import { Delete, Plus, Refresh } from '@element-plus/icons-vue';

defineProps({
  maps: { type: Array, default: () => [] },
  selectedMapId: { type: [Number, String, null], default: null },
  loading: { type: Boolean, default: false },
  deletable: { type: Boolean, default: false }
});

defineEmits(['select-map', 'create-map', 'delete-map', 'refresh-maps']);
</script>

<template>
  <aside class="sidebar">
    <div class="section-heading">
      <h2>思维导图</h2>
      <div class="heading-actions">
        <span>{{ maps.length }} 个</span>
        <el-button
          circle
          size="small"
          :icon="Refresh"
          :disabled="loading"
          aria-label="刷新思维导图列表"
          title="刷新思维导图列表"
          data-testid="refresh-map-list"
          @click="$emit('refresh-maps')"
        />
      </div>
    </div>
    <div class="sidebar-actions" v-if="deletable">
      <el-button class="wide-action" type="primary" :icon="Plus" data-testid="open-create-map" @click="$emit('create-map')">新增</el-button>
      <el-popconfirm title="确认删除当前思维导图及其节点？" confirm-button-text="删除" cancel-button-text="取消" confirm-button-type="danger" @confirm="$emit('delete-map')">
        <template #reference>
          <el-button class="wide-action" type="danger" plain :icon="Delete" data-testid="delete-selected-map" :disabled="loading || !selectedMapId">删除</el-button>
        </template>
      </el-popconfirm>
    </div>
    <div class="map-list">
      <button v-for="map in maps" :key="map.id" type="button" class="map-item" :class="{ active: map.id === selectedMapId }" :disabled="loading" @click="$emit('select-map', map.id)">
        <span>{{ map.title }}</span>
        <small>#{{ map.id }}</small>
      </button>
      <div v-if="maps.length === 0" class="empty small">暂无思维导图。</div>
    </div>
  </aside>
</template>

<style scoped>
.heading-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

.map-list {
  max-height: calc(100vh - 220px);
  overflow-y: auto;
}
</style>
