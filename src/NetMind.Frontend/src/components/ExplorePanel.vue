<script setup>
import { Close } from '@element-plus/icons-vue';
import RelationGraphCanvas from './RelationGraphCanvas.vue';

defineProps({
  /** 弹窗可见性（v-model） */
  modelValue: { type: Boolean, default: false },
  /** 探索路径（节点对象数组，尾节点为当前中心） */
  explorePath: { type: Array, default: () => [] },
  /** 当前探索深度 1~3 */
  exploreDepth: { type: Number, default: 2 },
  /** 是否正在加载 */
  exploreLoading: { type: Boolean, default: false },
  /** 探索错误信息（string|null） */
  exploreError: { type: String, default: null },
  /** 探索数据 { centerNode, nodes, relations } | null */
  exploreData: { type: Object, default: null },
  /** 当前中心节点（可能来自后端或路径尾节点） */
  exploreCenter: { type: Object, default: null }
});

const emit = defineEmits(['update:modelValue', 'set-depth', 'node-click', 'go-to-index', 'exit']);
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="知识探索"
    width="720px"
    append-to-body
    class="explore-dialog"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <div class="explore-panel">
      <!-- 顶部：路径面包屑 + 退出 -->
      <div class="explore-toolbar">
        <div class="explore-crumbs">
          <template v-for="(crumb, index) in explorePath" :key="crumb.id">
            <el-button
              link
              size="small"
              class="explore-crumb"
              :class="{ 'is-current': index === explorePath.length - 1 }"
              @click="emit('go-to-index', index)"
            >
              {{ crumb.title || `节点 #${crumb.id}` }}
            </el-button>
            <span v-if="index < explorePath.length - 1" class="explore-crumb-sep">/</span>
          </template>
          <span v-if="!explorePath.length" class="muted">暂无路径</span>
        </div>
        <el-button
          size="small"
          type="danger"
          plain
          :icon="Close"
          class="explore-exit"
          @click="() => { emit('exit'); emit('update:modelValue', false); }"
        >
          退出探索
        </el-button>
      </div>

      <!-- 深度切换 -->
      <div class="explore-depth-bar">
        <el-radio-group
          :model-value="exploreDepth"
          size="small"
          @update:model-value="(d) => emit('set-depth', d)"
        >
          <el-radio-button :value="1">1 层</el-radio-button>
          <el-radio-button :value="2">2 层</el-radio-button>
          <el-radio-button :value="3">3 层</el-radio-button>
        </el-radio-group>
        <span class="muted">选择向外探索的关联层数</span>
      </div>

      <!-- 主体：加载 / 错误 / 空态 / 图谱 -->
      <div class="explore-body" v-loading="exploreLoading">
        <el-alert
          v-if="exploreError"
          type="error"
          :title="exploreError"
          :closable="false"
          show-icon
        />
        <el-empty
          v-else-if="!exploreData || !exploreData.nodes || exploreData.nodes.length <= 1"
          description="该节点暂无关联知识"
        />
        <RelationGraphCanvas
          v-else
          :center-node="exploreCenter || exploreData.centerNode"
          :nodes="exploreData.nodes"
          :relations="exploreData.relations"
          :height="420"
          :node-draggable="false"
          @preview-node="(node) => emit('node-click', node)"
        />
      </div>

      <!-- 底部提示 -->
      <div v-if="exploreData" class="explore-footer">
        <span class="explore-footer-title">当前中心：{{ exploreCenter?.title || `节点 #${exploreCenter?.id}` }}</span>
        <span class="muted">{{ exploreData.nodes.length }} 个节点 · {{ exploreData.relations.length }} 条关系</span>
      </div>
    </div>
  </el-dialog>
</template>

<style scoped>
.explore-panel { display: flex; flex-direction: column; gap: 12px; }
.explore-toolbar { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
.explore-crumbs { display: flex; align-items: center; flex-wrap: wrap; gap: 2px; min-width: 0; }
.explore-crumb { font-size: 13px; color: var(--el-text-color-regular); padding: 2px 4px; }
.explore-crumb.is-current { color: var(--el-color-primary); font-weight: 600; }
.explore-crumb-sep { color: var(--el-text-color-placeholder); font-size: 12px; margin: 0 3px; }
.explore-exit { flex-shrink: 0; }
.explore-depth-bar { display: flex; align-items: center; gap: 10px; }
.explore-body { min-height: 260px; border: 1px solid var(--el-border-color-lighter); border-radius: 6px; overflow: hidden; }
.explore-footer { display: flex; justify-content: space-between; align-items: center; font-size: 12px; }
.explore-footer-title { color: var(--el-text-color-primary); font-weight: 500; }
.muted { color: var(--el-text-color-secondary); font-size: 12px; }
</style>