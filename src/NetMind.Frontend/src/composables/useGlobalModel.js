/**
 * 全局 AI 模型配置管理
 *
 * 从后端 API + localStorage 合并模型列表，
 * 并提供全局选中的模型配置（endpoint、provider、model、apiKey）。
 * 所有 AI 调用统一从此处读取。
 */

import { ref } from 'vue';
import { api } from '../services/api';

const STORAGE_KEY_CUSTOM_MODELS = 'netmind_custom_models';
const STORAGE_KEY_SELECTED_MODEL = 'netmind_selected_model_id';
const STORAGE_KEY_BACKEND_KEYS = 'netmind_backend_model_keys';

// 响应式状态（模块级单例）
const backendModels = ref([]);
const customModels = ref([]);
const selectedModelId = ref('');
const backendKeyOverrides = ref({});
const loaded = ref(false);

function allModels() {
  return [...backendModels.value, ...customModels.value];
}

function getSelectedModel() {
  return allModels().find(m => m.id === selectedModelId.value) ?? null;
}

function loadBackendKeyOverrides() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_BACKEND_KEYS);
    backendKeyOverrides.value = raw ? JSON.parse(raw) : {};
  } catch {
    backendKeyOverrides.value = {};
  }
}

function loadCustomModels() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_CUSTOM_MODELS);
    const models = raw ? JSON.parse(raw) : [];
    customModels.value = models.map((model) => ({
      ...model,
      model: model.model || 'deepseek-chat'
    }));
  } catch {
    customModels.value = [];
  }
}

function loadSelection() {
  const saved = localStorage.getItem(STORAGE_KEY_SELECTED_MODEL);
  if (saved && allModels().some(m => m.id === saved)) {
    selectedModelId.value = saved;
  } else {
    const first = allModels().find(m => m.status === 'enabled' || m.enabled);
    selectedModelId.value = first?.id ?? '';
    if (selectedModelId.value) {
      localStorage.setItem(STORAGE_KEY_SELECTED_MODEL, selectedModelId.value);
    }
  }
}

/**
 * 加载所有模型并选择默认。
 * 在应用启动时调用一次（App.vue onMounted）。
 */
export async function loadGlobalModels() {
  try {
    const data = await api('/api/ai/models');
    backendModels.value = data || [];
  } catch {
    backendModels.value = [];
  }

  loadCustomModels();
  loadBackendKeyOverrides();
  loadSelection();
  loaded.value = true;
}

/**
 * 刷新模型列表（设置变更后调用）。
 */
export async function refreshGlobalModels() {
  await loadGlobalModels();
}

/**
 * 获取全局选中模型的信息（用于 UI 展示）。
 */
export function useGlobalModel() {
  return {
    allModels,
    getSelectedModel,
    selectedModelId,
    backendModels,
    customModels,
    loaded
  };
}

/**
 * 获取当前全局选中模型的完整 API 调用配置。
 * 用于所有 AI API 请求。
 *
 * @returns {{ modelId: string|null, endpoint: string|null, provider: string|null, model: string|null, apiKey: string|null, name: string|null }}
 */
export function getGlobalModelConfig() {
  loadCustomModels();
  loadBackendKeyOverrides();
  loadSelection();

  const model = getSelectedModel();
  if (!model) {
    return { modelId: null, endpoint: null, provider: null, model: null, apiKey: null, name: null };
  }

  const modelId = model.id;
  const provider = model.provider || model.Provider || 'deepseek';
  const endpoint = model.endpoint || model.Endpoint || '';
  const modelName = model.model || model.Model || '';

  // API Key 优先级：自定义模型直接取 apiKey > 后端模型覆盖 Key
  let apiKey = null;
  if (modelId && modelId.startsWith('custom-')) {
    apiKey = model.apiKey || null;
  } else {
    apiKey = backendKeyOverrides.value[modelId] || null;
  }

  return {
    modelId,
    endpoint,
    provider,
    model: modelName,
    apiKey,
    name: model.name || model.Name || ''
  };
}

/**
 * 获取合并后的完整模型列表（含自定义模型）。
 * 用于替代原有的 loadAiModels()。
 */
export function getMergedModels() {
  loadCustomModels();
  return allModels();
}
