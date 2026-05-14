<script setup>
import { ref, watch, onMounted } from 'vue';
import { Setting, Plus, Delete, Check } from '@element-plus/icons-vue';
import { api } from '../services/api';

const props = defineProps({
  modelValue: { type: Boolean, default: false }
});

const emit = defineEmits(['update:modelValue', 'model-changed']);

// ---------- 常量 ----------
const STORAGE_KEY_MODELS = 'netmind_custom_models';
const STORAGE_KEY_CONTEXT = 'netmind_context_length';
const STORAGE_KEY_SELECTED_MODEL = 'netmind_selected_model_id';
const STORAGE_KEY_AGENTBUILD_PATH = 'netmind_agentbuild_path';
const DEFAULT_AGENTBUILD_PATH = 'G:\\AAW+\\NetMind\\AgentBuild';

// ---------- 后端模型列表 ----------
const backendModels = ref([]);

// ---------- 自定义模型 ----------
const customModels = ref([]);
const editingModel = ref(null);
const showModelForm = ref(false);
const modelForm = ref({ name: '', endpoint: '', apiKey: '' });

// ---------- 全局默认模型 ----------
const selectedModelId = ref('');

// 合并后的全部模型列表
function allModels() {
  return [...backendModels.value, ...customModels.value];
}

function getModelById(id) {
  return allModels().find(m => m.id === id);
}

function getSelectedModel() {
  return getModelById(selectedModelId.value);
}

function selectModel(id) {
  selectedModelId.value = id;
  localStorage.setItem(STORAGE_KEY_SELECTED_MODEL, id);
  emit('model-changed');
}

async function loadBackendModels() {
  try {
    const data = await api('/api/ai/models');
    backendModels.value = data || [];
  } catch {
    backendModels.value = [];
  }
}

function loadCustomModels() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_MODELS);
    customModels.value = raw ? JSON.parse(raw) : [];
  } catch {
    customModels.value = [];
  }
}

function saveCustomModels() {
  localStorage.setItem(STORAGE_KEY_MODELS, JSON.stringify(customModels.value));
}

function loadSelection() {
  const saved = localStorage.getItem(STORAGE_KEY_SELECTED_MODEL);
  if (saved && allModels().some(m => m.id === saved)) {
    selectedModelId.value = saved;
  } else {
    // 默认选第一个可用模型
    const first = allModels().find(m => m.status === 'enabled' || m.enabled);
    selectedModelId.value = first?.id ?? '';
    if (selectedModelId.value) {
      localStorage.setItem(STORAGE_KEY_SELECTED_MODEL, selectedModelId.value);
    }
  }
}

// ---------- 自定义模型 CRUD ----------
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
    enabled: true,
    status: 'enabled'
  };
  if (editingModel.value !== null) {
    const orig = customModels.value[editingModel.value];
    entry.id = orig.id;
    customModels.value[editingModel.value] = entry;
  } else {
    customModels.value.push(entry);
  }
  saveCustomModels();
  showModelForm.value = false;

  // 如果是第一个模型，自动选中
  if (allModels().length === 1 && !selectedModelId.value) {
    selectModel(entry.id);
  }
}

function deleteModel(index) {
  const deleted = customModels.value[index];
  customModels.value.splice(index, 1);
  saveCustomModels();

  // 如果删除的是当前选中模型，切换到第一个
  if (selectedModelId.value === deleted.id) {
    const first = allModels().find(m => m.status === 'enabled' || m.enabled);
    selectModel(first?.id ?? '');
  }
}

// ---------- 后端模型的 API Key 覆盖 ----------
const backendKeyOverrides = ref({});

function loadBackendKeyOverrides() {
  try {
    const raw = localStorage.getItem('netmind_backend_model_keys');
    backendKeyOverrides.value = raw ? JSON.parse(raw) : {};
  } catch {
    backendKeyOverrides.value = {};
  }
}

function getBackendKeyOverride(modelId) {
  return backendKeyOverrides.value[modelId] || '';
}

function setBackendKeyOverride(modelId, key) {
  backendKeyOverrides.value[modelId] = key;
  localStorage.setItem('netmind_backend_model_keys', JSON.stringify(backendKeyOverrides.value));
}

// ---------- 上下文长度 ----------
const contextLength = ref(51200);

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

// ---------- AgentBuild 脚本路径 ----------
const agentBuildPath = ref(DEFAULT_AGENTBUILD_PATH);

function loadAgentBuildPath() {
  try {
    agentBuildPath.value = localStorage.getItem(STORAGE_KEY_AGENTBUILD_PATH) || DEFAULT_AGENTBUILD_PATH;
  } catch {
    agentBuildPath.value = DEFAULT_AGENTBUILD_PATH;
  }
}

function saveAgentBuildPath(val) {
  agentBuildPath.value = val;
  localStorage.setItem(STORAGE_KEY_AGENTBUILD_PATH, val);
}

// ---------- 生命周期 ----------
watch(() => props.modelValue, async (val) => {
  if (val) {
    loadCustomModels();
    loadBackendKeyOverrides();
    loadContextLength();
    loadAgentBuildPath();
    await loadBackendModels();
    loadSelection();
  }
});
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="设置"
    width="min(620px, calc(100vw - 32px))"
    class="settings-dialog"
    :close-on-click-modal="false"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <div class="settings-body">
      <!-- ===== 全局默认模型 ===== -->
      <section class="settings-section">
        <div class="section-heading">
          <h3>全局默认 AI 模型</h3>
        </div>
        <p class="helper-text">
          选择全局默认 AI 模型。所有 AI 功能（AI 清洗、节点问答、全图问答、应用帮助等）都将使用此模型。
        </p>

        <div v-if="allModels().length === 0" class="empty small">
          暂无可用模型。请在下方「AI 大模型配置」中添加自定义模型，或确保后端配置了默认模型。
        </div>

        <div v-else class="model-select-list">
          <div
            v-for="model in allModels()"
            :key="model.id"
            class="model-select-item"
            :class="{ selected: selectedModelId === model.id }"
            @click="selectModel(model.id)"
          >
            <div class="model-select-radio">
              <el-icon v-if="selectedModelId === model.id" :size="16" color="var(--el-color-primary)"><Check /></el-icon>
              <span v-else class="radio-empty"></span>
            </div>
            <div class="model-select-info">
              <span class="model-select-name">{{ model.name }}</span>
              <span class="model-select-detail">
                {{ model.provider || model.Provider }} · {{ model.endpoint || model.Endpoint }}
              </span>
            </div>
            <el-tag v-if="model.id && model.id.startsWith('custom-')" size="small" type="warning">自定义</el-tag>
            <el-tag v-else size="small" type="info">内置</el-tag>
          </div>
        </div>
      </section>

      <!-- ===== 内置模型 API Key 覆盖 ===== -->
      <section v-if="backendModels.length > 0" class="settings-section">
        <div class="section-heading">
          <h3>内置模型 API Key</h3>
        </div>
        <p class="helper-text">
          为内置模型覆盖 API Key（替代环境变量）。留空则使用服务器配置的环境变量。
        </p>
        <div v-for="model in backendModels" :key="model.id" class="backend-key-row">
          <span class="backend-key-label">{{ model.name }}</span>
          <el-input
            :model-value="getBackendKeyOverride(model.id)"
            type="password"
            show-password
            size="small"
            placeholder="留空使用环境变量"
            style="flex: 1;"
            @update:model-value="setBackendKeyOverride(model.id, $event)"
          />
        </div>
      </section>

      <!-- ===== AI 大模型配置（自定义） ===== -->
      <section class="settings-section">
        <div class="section-heading">
          <h3>AI 大模型配置</h3>
          <el-button :icon="Plus" size="small" @click="addModel">新增模型</el-button>
        </div>
        <p class="helper-text">
          配置自定义 AI 模型的名称、地址和 API Key。API Key 和模型配置仅保存在浏览器本地，不会上传至服务器或提交到仓库。
        </p>

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
          <p class="helper-text">API Key 仅保存在浏览器本地 localStorage，不会提交到 Git 仓库或上传到服务器。</p>
        </div>
        <template #footer>
          <el-button @click="showModelForm = false">取消</el-button>
          <el-button type="primary" @click="saveModel">保存</el-button>
        </template>
      </el-dialog>

      <!-- ===== AgentBuild 脚本设置 ===== -->
      <section class="settings-section">
        <div class="section-heading">
          <h3>AgentBuild 脚本设置</h3>
        </div>
        <p class="helper-text">
          配置 AgentBuild 根目录。AI Agent 功能会调用该目录下的 <code>src/agent_kernel.py</code>。
        </p>
        <el-input
          :model-value="agentBuildPath"
          placeholder="例如：G:\AAW+\NetMind\AgentBuild"
          @update:model-value="saveAgentBuildPath"
        />
      </section>

      <!-- ===== 上下文长度 ===== -->
      <section class="settings-section">
        <div class="section-heading">
          <h3>AI 对话上下文设置</h3>
        </div>
        <p class="helper-text">
          上下文长度决定了 API 调用时传递的历史消息量。值越大信息越准确，但速度越慢，Token 消耗越快。
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

/* ---------- 全局模型选择 ---------- */
.model-select-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.model-select-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  cursor: pointer;
  transition: border-color 0.2s, background 0.2s;
}

.model-select-item:hover {
  border-color: var(--el-color-primary-light-5);
  background: var(--el-color-primary-light-9);
}

.model-select-item.selected {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.model-select-radio {
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.radio-empty {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  border: 2px solid var(--el-border-color);
}

.model-select-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
  flex: 1;
}

.model-select-name {
  font-weight: 600;
  font-size: 14px;
}

.model-select-detail {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ---------- 内置模型 Key 覆盖 ---------- */
.backend-key-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.backend-key-label {
  width: 120px;
  flex-shrink: 0;
  font-size: 13px;
  font-weight: 500;
}

/* ---------- 自定义模型列表 ---------- */
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

/* ---------- 上下文长度 ---------- */
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
