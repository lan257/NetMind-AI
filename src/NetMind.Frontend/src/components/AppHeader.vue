<script setup>
import { ref } from 'vue';
import { Back, Search } from '@element-plus/icons-vue';
import { renderMarkdown } from '../composables/useMarkdown';

const props = defineProps({
  page: { type: String, required: true },
  searchNodes: { type: Function, default: null }
});

const emit = defineEmits(['go-main', 'jump-to-node']);

const searchKeyword = ref('');
const searchResults = ref([]);
const searching = ref(false);

async function handleSearch(query) {
  if (!query) {
    searchResults.value = [];
    return;
  }
  searching.value = true;
  try {
    searchResults.value = await props.searchNodes(query);
  } finally {
    searching.value = false;
  }
}

function handleSelect(id) {
  const node = searchResults.value.find(n => n.id === id);
  if (node) {
    emit('jump-to-node', { mapId: node.mapId, nodeId: node.id });
    searchKeyword.value = '';
    searchResults.value = [];
  }
}
</script>

<template>
  <header class="topbar">
    <div class="brand">
      <p class="eyebrow">P2.0</p>
      <h1>NetMind</h1>
    </div>

    <div class="header-center">
      <el-select
        v-model="searchKeyword"
        filterable
        remote
        reserve-keyword
        placeholder="搜索全库节点..."
        :remote-method="handleSearch"
        :loading="searching"
        class="global-search"
        @change="handleSelect"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
        <el-option
          v-for="item in searchResults"
          :key="item.id"
          :label="item.title"
          :value="item.id"
        >
          <el-tooltip
            effect="dark"
            placement="right"
            :show-after="300"
          >
            <template #content>
              <div class="search-preview-tooltip">
                <div class="tooltip-map-tag" v-if="item.mapTitle">
                  所属导图：{{ item.mapTitle }}
                </div>
                <div v-if="item.content" class="markdown-body mini" v-html="renderMarkdown(item.content)"></div>
                <div v-else class="muted">暂无详细内容</div>
              </div>
            </template>
            <div class="search-result-item">
              <span class="title">{{ item.title }}</span>
              <span class="meta">#{{ item.id }} · {{ item.mapTitle }}</span>
            </div>
          </el-tooltip>
        </el-option>
      </el-select>
    </div>

    <div class="topbar-actions">
      <el-button v-if="page === 'create'" :icon="Back" @click="$emit('go-main')">返回主页面</el-button>
    </div>
  </header>
</template>

<style scoped>
.brand {
  flex-shrink: 0;
}

.header-center {
  flex: 1;
  max-width: 600px;
  margin: 0 20px;
}

.global-search {
  width: 100%;
}

:deep(.global-search .el-input__wrapper) {
  border-radius: 20px;
  background-color: #f0f2f5;
  box-shadow: none !important;
}

:deep(.global-search .el-input__wrapper:hover) {
  background-color: #e4e7ed;
}

.search-result-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
  gap: 12px;
}

.search-result-item .title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 320px;
}

.search-result-item .meta {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  flex-shrink: 0;
}
</style>
