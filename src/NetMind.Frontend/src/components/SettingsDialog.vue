<script setup>
import { ref, watch } from 'vue';
import { Setting, Plus, Delete } from '@element-plus/icons-vue';

const props = defineProps({
  modelValue: { type: Boolean, default: false }
});

const emit = defineEmits(['update:modelValue']);

// ---------- AI 模型配置 ----------
const STORAGE_KEY_MODELS = 'netmind_custom_models';
const STORAGE_KEY_CONTEXT = 'netmind_context_length';

const customModels = ref([]);
const editingModel = ref(null);
const showModelForm = ref(false);
const modelForm = ref({ name: '', endpoint: '', apiKey: '' });

function loadModels() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_MODELS);
    customModels.value = raw ? JSON.parse(raw) : [];
  } catch {
    customModels.value = [];
  }
}

function saveModels() {
  localStorage.setItem(STORAGE_KEY_MODELS, JSON.stringify(customModels.value));
}

function addModel() {
  modelForm.value = { name: '', endpoint: '', apiKey: '' };
  editingModel.value = null;
  showModelForm.value = true;
}

function editModel(index) {
  const m = customModels.value[index];
  modelForm.value = { name: m.name, endpoint: m.endpoint, apiKey: m.apiKey };
  editingModel.value = index;
  showModelForm.value = true;
}

function saveModel() {
  if (!modelForm.value.name.trim() || !modelForm.value.endpoint.trim()) {
    return;
  }
  const entry = {
    id: 'custom-' + Date.now(),
    name: modelForm.value.name.trim(),
    endpoint: modelForm.value.endpoint.trim(),
    apiKey: modelForm.value.apiKey,
    provider: 'deepseek',
    isDefault: false,
    enabled: true
  };
  if (editingModel.value !== null) {
    // Keep the original id and position
    const orig = customModels.value[editingModel.value];
    entry.id = orig.id;
    customModels.value[editingModel.value] = entry;
  } else {
    customModels.value.push(entry);
  }
  saveModels();
  showModelForm.value = false;
}

function deleteModel(index) {
  customModels.value.splice(index, 1);
  saveModels();
}

// ---------- 上下文长度 ----------
const contextLength = ref(51200); // 50K default

function loadContextLength() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_CONTEXT);
    contextLength.value = raw ? parseInt(raw, 10) : 51200;
  } catch {
    contextLength.value = 51200;
  }
}

function saveContextLength(val) {
  contextLength.value = val;
  localStorage.setItem(STORAGE_KEY_CONTEXT, String(val));
}

function formatContextLength(val) {
  if (val >= 1000000) return (val / 1000000).toFixed(1) + 'M';
  if (val >= 1000) return (val / 1000).toFixed(val % 1000 === 0 ? 0 : 1) + 'K';
  return val + '';
}

watch(() => props.modelValue, (val) => {
  if (val) {
    loadModels();
    loadContextLength();
  }
});
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="设置"
    width="min(560px, calc(100vw - 32px))"
    class="settings-dialog"
    :close-on-click-modal="false"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <div class="settings-body">
      <!-- AI 模型配置 -->
      <section class="settings-section">
        <div class="section-heading">
          <h3>AI 大模型配置</h3>
          <el-button :icon="Plus" size="small" @click="addModel">新增模型</el-button>
        </div>
        <p class="helper-text">配置自定义 AI 模型的名称、地址和 API Key。API Key 仅保存在浏览器本地，不会上传至服务器或提交到仓库。</p>

        <div v-if="customModels.length === 0" class="empty small">暂无自定义模型。</div>
        <div v-else class="model-list">
          <div v-for="(model, index) in customModels" :key="model.id" class="model-item">
            <div class="model-info">
              <span class="model-name">{{ model.name }}</span>
              <span class="model-endpoint">{{ model.endpoint }}</span>
              <span class="model-key-hint">{{ model.apiKey ? '已配置 Key' : '未配置 Key' }}</span>
            </div>
            <div class="model-actions">
              <el-button size="small" link @click="editModel(index)">编辑</el-button>
              <el-button size="small" link type="danger" :icon="Delete" @click="deleteModel(index)">删除</el-button>
            </div>
          </div>
        </div>
      </section>

      <!-- 模型编辑表单 -->
      <el-dialog
        v-model="showModelForm"
        :title="editingModel !== null ? '编辑模型' : '新增模型'"
        width="min(420px, calc(100vw - 32px))"
        append-to-body
      >
        <div class="model-form">
          <label>
            模型名称
            <el-input v-model="modelForm.name" placeholder="例如：我的 DeepSeek 模型" />
          </label>
          <label>
            API 地址
            <el-input v-model="modelForm.endpoint" placeholder="例如：https://api.deepseek.com/chat/completions" />
          </label>
          <label>
            API Key
            <el-input v-model="modelForm.apiKey" type="password" show-password placeholder="输入你的 API Key" />
          </label>
          <p class="helper-text">API Key 仅保存在浏览器本地 localStorage，不会提交到 Git 仓库。</p>
        </div>
        <template #footer>
          <el-button @click="showModelForm = false">取消</el-button>
          <el-button type="primary" @click="saveModel">保存</el-button>
        </template>
      </el-dialog>

      <!-- 上下文长度 -->
      <section class="settings-section">
        <div class="section-heading">
          <h3>AI 对话上下文设置</h3>
        </div>
        <p class="helper-text">
          上下文长度决定了 API 调用时传递的历史消息量。值越大信息越准确，但速度越慢，Token 消耗越快。
          当前为配置项，暂未启用。
        </p>
        <div class="context-setting">
          <div class="context-slider-row">
            <el-slider
              v-model="contextLength"
              :min="1024"
              :max="1048576"
              :step="1024"
              :show-tooltip="false"
              style="flex: 1;"
              @change="saveContextLength"
            />
            <el-input-number
              :model-value="contextLength"
              :min="1024"
              :max="1048576"
              :step="1024"
              style="width: 140px; flex-shrink: 0;"
              @update:model-value="saveContextLength"
            />
          </div>
          <div class="context-marks">
            <span>最小值：1K</span>
            <span class="recommend">推荐值：50K</span>
            <span>最大值：1M</span>
          </div>
          <div class="context-current">
            当前值：<strong>{{ formatContextLength(contextLength) }}</strong>
            <span v-if="contextLength === 51200" class="recommend-tag">推荐</span>
          </div>
        </div>
      </section>
    </div>
  </el-dialog>
</template>

<style scoped>
.settings-body {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.settings-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.settings-section .section-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0;
}

.settings-section .section-heading h3 {
  margin: 0;
  font-size: 16px;
}

.helper-text {
  margin: 0;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  line-height: 1.5;
}

.model-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.model-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  background: var(--el-fill-color-light);
}

.model-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.model-name {
  font-weight: 600;
  font-size: 14px;
}

.model-endpoint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: min(280px, 40vw);
}

.model-key-hint {
  font-size: 11px;
  color: var(--el-color-success);
}

.model-actions {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.model-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.model-form label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 14px;
  font-weight: 500;
}

.context-setting {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  background: var(--el-fill-color-lighter);
}

.context-slider-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.context-marks {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.recommend {
  color: var(--el-color-primary);
  font-weight: 600;
}

.context-current {
  font-size: 13px;
}

.recommend-tag {
  display: inline-block;
  margin-left: 6px;
  padding: 1px 6px;
  font-size: 11px;
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  border-radius: 4px;
  font-weight: 600;
}
</style>
