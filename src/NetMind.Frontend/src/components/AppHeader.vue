<script setup>
import { Back } from '@element-plus/icons-vue';

defineProps({
  page: { type: String, required: true }
});

defineEmits(['go-main']);
</script>

<template>
  <header class="topbar">
    <div>
      <p class="eyebrow">P2.0</p>
      <h1>NetMind</h1>
    </div>
    <div class="topbar-actions">
      <el-button v-if="page === 'create'" :icon="Back" @click="$emit('go-main')">返回主页面</el-button>
    </div>
  </header>
</template>
