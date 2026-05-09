<script setup>
import { ref, nextTick, watch } from 'vue';
import { Link } from '@element-plus/icons-vue';
import { renderMarkdown } from '../composables/useMarkdown';

const props = defineProps({
  selectedMap: { type: Object, default: null },
  selectedNode: { type: Object, default: null },
  nodeForm: { type: Object, required: true },
  relationForm: { type: Object, required: true },
  candidateTargets: { type: Array, default: () => [] },
  selectedNodeRelations: { type: Array, default: () => [] },
  nodeTitleById: { type: Object, required: true },
  loading: { type: Boolean, default: false },
  searchNodes: { type: Function, default: null }
});

defineEmits([
  'create-root',
  'create-child',
  'save-node',
  'delete-node',
  'delete-subtree',
  'create-relation',
  'delete-relation'
]);

const searchKeyword = ref('');
const searchResults = ref([]);
const searching = ref(false);
const showRefDialog = ref(false);
const refTriggerPos = ref({ start: -1, end: -1 });

async function handleSearch(query) {
  if (!query) {
    searchResults.value = [];
    return;
  }
  searching.value = true;
  try {
    const results = await props.searchNodes(query);
    searchResults.value = results.filter(n => n.id !== props.selectedNode?.id);
  } finally {
    searching.value = false;
  }
}

function insertReference(node) {
  const refText = `[[${node.title}|${node.id}]]`;
  const content = props.nodeForm.content || '';

  if (refTriggerPos.value.start >= 0) {
    const before = content.slice(0, refTriggerPos.value.start);
    const after = content.slice(refTriggerPos.value.end);
    props.nodeForm.content = before + refText + after;
  } else {
    props.nodeForm.content = content + (content.length > 0 && !content.endsWith('\n') ? '\n' : '') + refText;
  }

  showRefDialog.value = false;
  searchKeyword.value = '';
  searchResults.value = [];
  refTriggerPos.value = { start: -1, end: -1 };
}

function onContentKeyup(event) {
  const el = event.target;
  if (!el) return;
  const pos = el.selectionStart;
  const text = el.value || '';
  if (pos >= 2 && text.slice(pos - 2, pos) === '[[') {
    refTriggerPos.value = { start: pos - 2, end: pos };
    showRefDialog.value = true;
  }
}

function onRefDialogOpened() {
  nextTick(() => {
    const input = document.querySelector('.ref-dialog-select-wrap .el-select__input');
    if (input) input.focus();
  });
}
</script>

<template>
  <aside class="inspector">
    <div class="section-heading">
      <h2>节点编辑</h2>
      <span>{{ selectedNode ? `#${selectedNode.id}` : '未选择' }}</span>
    </div>
    <p class="helper-text">
      先在左侧列表选择节点。修改内容后点保存；新增子节点会挂到当前选中节点下面。
    </p>

    <label>
      节点标题
      <el-input v-model="nodeForm.title" data-testid="node-title" placeholder="例如：用户注册流程" />
    </label>
    <label class="content-label">
      <div class="label-header">
        <span>节点内容</span>
        <el-button
          v-if="selectedNode"
          link
          type="primary"
          :icon="Link"
          @click="showRefDialog = true"
        >
          插入引用
        </el-button>
      </div>
      <el-input
        v-model="nodeForm.content"
        data-testid="node-content"
        type="textarea"
        :rows="5"
        placeholder="记录这个节点的说明、结论或待办。使用 [[标题|ID]] 引用其他节点。"
        @keyup="onContentKeyup"
      />
    </label>

    <el-dialog v-model="showRefDialog" title="插入节点引用 (全局)" width="min(400px, calc(100vw - 32px))" append-to-body @opened="onRefDialogOpened">
      <el-select
        v-model="searchKeyword"
        filterable
        remote
        reserve-keyword
        placeholder="输入关键词搜索全库节点"
        :remote-method="handleSearch"
        :loading="searching"
        style="width: 100%"
        popper-class="ref-dialog-select"
        class="ref-dialog-select-wrap"
        @change="(id) => {
          const node = searchResults.find(n => n.id === id);
          if (node) insertReference(node);
        }"
      >
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
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
              <span style="overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 240px;">{{ item.title }}</span>
              <span style="color: var(--el-text-color-secondary); font-size: 12px; margin-left: 8px;">#{{ item.id }}</span>
            </div>
          </el-tooltip>
        </el-option>
      </el-select>
      <template #footer>
        <el-button @click="showRefDialog = false">取消</el-button>
      </template>
    </el-dialog>

    <label>
      同级排序
      <el-input-number v-model="nodeForm.orderNo" data-testid="node-order" :min="0" />
    </label>

    <div class="button-grid">
      <el-button data-testid="create-root-node" :disabled="loading || !selectedMap" @click="$emit('create-root')">
        新增根节点
      </el-button>
      <el-button data-testid="create-child-node" :disabled="loading || !selectedMap" @click="$emit('create-child')">
        新增子节点
      </el-button>
      <el-button type="primary" data-testid="save-node" :disabled="loading || !selectedNode" @click="$emit('save-node')">
        保存当前节点
      </el-button>
      <el-button :disabled="loading || !selectedNode" @click="$emit('delete-node')">删除节点</el-button>
      <el-button type="danger" :disabled="loading || !selectedNode" @click="$emit('delete-subtree')">
        删除子树
      </el-button>
    </div>

    <div class="section-heading relation-title">
      <h2>节点关联</h2>
      <span>{{ selectedNodeRelations.length }} 条</span>
    </div>
    <p class="helper-text">关联用于表达两个节点之间的额外关系，不会改变层级父子结构。</p>

    <label>
      目标节点
      <el-select v-model="relationForm.targetId" data-testid="relation-target" :disabled="!selectedNode" placeholder="请选择目标节点">
        <el-option v-for="node in candidateTargets" :key="node.id" :label="node.title" :value="node.id" />
      </el-select>
    </label>
    <label>
      关系类型
      <el-input v-model="relationForm.relationType" data-testid="relation-type" placeholder="例如：relates_to" />
    </label>
    <label>
      权重
      <el-input-number v-model="relationForm.weight" data-testid="relation-weight" :min="0" :step="0.1" />
    </label>
    <el-button data-testid="create-relation" :disabled="loading || !selectedNode" @click="$emit('create-relation')">
      新增关联
    </el-button>

    <div class="relation-list">
      <div v-for="relation in selectedNodeRelations" :key="relation.id" class="relation-row">
        <span>
          {{ nodeTitleById.get(relation.sourceId) ?? `#${relation.sourceId}` }}
          ->
          {{ nodeTitleById.get(relation.targetId) ?? `#${relation.targetId}` }}
          · {{ relation.relationType }}
        </span>
        <el-button size="small" :disabled="loading" @click="$emit('delete-relation', relation.id)">删除</el-button>
      </div>
      <div v-if="selectedNode && selectedNodeRelations.length === 0" class="empty small">当前节点暂无关联。</div>
    </div>
  </aside>
</template>

<style scoped>
.inspector {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  background: #fff;
  border-left: 1px solid var(--el-border-color-light);
  overflow-y: auto;
}

.section-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 8px;
}

.section-heading h2 {
  font-size: 16px;
  margin: 0;
}

.section-heading span {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.relation-title {
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid var(--el-border-color-lighter);
}

.helper-text {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin: 0 0 8px 0;
  line-height: 1.4;
}

label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 14px;
  font-weight: 500;
}

.label-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.button-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  margin-top: 8px;
}

.relation-list {
  margin-top: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.relation-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
  font-size: 13px;
}

.empty.small {
  text-align: center;
  color: var(--el-text-color-placeholder);
  padding: 16px;
  font-size: 13px;
}
</style>
