<script setup>
defineProps({
  selectedMap: { type: Object, default: null },
  selectedNode: { type: Object, default: null },
  nodeForm: { type: Object, required: true },
  relationForm: { type: Object, required: true },
  candidateTargets: { type: Array, default: () => [] },
  selectedNodeRelations: { type: Array, default: () => [] },
  nodeTitleById: { type: Object, required: true },
  loading: { type: Boolean, default: false }
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
    <label>
      节点内容
      <el-input
        v-model="nodeForm.content"
        data-testid="node-content"
        type="textarea"
        :rows="5"
        placeholder="记录这个节点的说明、结论或待办。"
      />
    </label>
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
