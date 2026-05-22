import { computed, ref } from 'vue';
import { api, downloadUrl } from '../services/api';
import { getGlobalModelConfig, useGlobalModel } from './useGlobalModel';

function createConversationId() {
  if (window.crypto?.randomUUID) {
    return window.crypto.randomUUID();
  }

  return `chat-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export function useMindMapWorkspace() {
  const maps = ref([]);
  const nodes = ref([]);
  const relations = ref([]);
  const selectedMapId = ref(null);
  const selectedNodeId = ref(null);
  const mapTitle = ref('');
  const nodeForm = ref({ title: '', content: '', orderNo: 1 });
  const relationForm = ref({ targetId: '', relationType: 'relates_to', weight: 1 });
  const transferText = ref('');
  const importTitleOverride = ref('');
  const fileInput = ref(null);
  const aiModels = ref([]);
  const selectedAiModelId = ref('');
  const naturalLanguageInput = ref('');
  const aiStatus = ref('');
  const chatOpen = ref(false);
  const chatInput = ref('');
  const chatMessages = ref([]);
  const chatConversationId = ref(createConversationId());
  const chatHistoryOpen = ref(false);
  const chatHistoryLoading = ref(false);
  const chatHistoryGroups = ref([]);
  const loading = ref(false);
  const toast = ref({ type: '', text: '' });

  const { allModels, selectedModelId: globalSelectedId } = useGlobalModel();

  const selectedMap = computed(() => maps.value.find((map) => map.id === selectedMapId.value) ?? null);
  const selectedNode = computed(() => nodes.value.find((node) => node.id === selectedNodeId.value) ?? null);
  const candidateTargets = computed(() => nodes.value.filter((node) => node.id !== selectedNodeId.value));
  const selectedNodeRelations = computed(() => {
    if (!selectedNode.value) {
      return [];
    }

    return relations.value.filter(
      (relation) => relation.sourceId === selectedNode.value.id || relation.targetId === selectedNode.value.id
    );
  });
  const nodeTitleById = computed(() => {
    const result = new Map();
    nodes.value.forEach((node) => result.set(node.id, node.title));
    return result;
  });
  const childCountByParent = computed(() => {
    const counts = new Map();
    nodes.value.forEach((node) => {
      const key = node.parentId ?? 0;
      counts.set(key, (counts.get(key) ?? 0) + 1);
    });
    return counts;
  });
  const visualNodes = computed(() => {
    const byParent = new Map();
    nodes.value.forEach((node) => {
      const key = node.parentId ?? 0;
      if (!byParent.has(key)) {
        byParent.set(key, []);
      }
      byParent.get(key).push(node);
    });

    byParent.forEach((items) => {
      items.sort((left, right) => left.orderNo - right.orderNo || left.id - right.id);
    });

    const result = [];
    const walk = (parentId, depth) => {
      (byParent.get(parentId) ?? []).forEach((node) => {
        result.push({ ...node, depth, childCount: childCountByParent.value.get(node.id) ?? 0 });
        walk(node.id, depth + 1);
      });
    };

    walk(0, 0);
    return result;
  });
  const chatContextText = computed(() => buildConversationContext());

  function showToast(type, text) {
    toast.value = { type, text };
    window.setTimeout(() => {
      if (toast.value.text === text) {
        toast.value = { type: '', text: '' };
      }
    }, 3200);
  }

  async function run(action, successMessage = '') {
    loading.value = true;
    try {
      const result = await action();
      if (successMessage) {
        showToast('success', successMessage);
      }
      return result;
    } catch (error) {
      showToast('error', error instanceof Error ? error.message : '操作失败');
      return null;
    } finally {
      loading.value = false;
    }
  }

  function resetNodeForm() {
    nodeForm.value = { title: '', content: '', orderNo: 1 };
  }

  function fillNodeForm(node) {
    nodeForm.value = node
      ? { title: node.title, content: node.content ?? '', orderNo: node.orderNo }
      : { title: '', content: '', orderNo: 1 };
  }

  async function refreshMapData(mapId, options = {}) {
    const keepNodeId = options.keepNodeId ?? selectedNodeId.value;
    const result = await run(async () => {
      const [nodeData, relationData] = await Promise.all([
        api(`/api/nodes/by-map/${mapId}`),
        api(`/api/node-relations/by-map/${mapId}`)
      ]);
      return { nodeData, relationData };
    }, options.message ?? '');

    if (!result) {
      nodes.value = [];
      relations.value = [];
      selectedNodeId.value = null;
      resetNodeForm();
      return;
    }

    nodes.value = result.nodeData;
    relations.value = result.relationData;

    if (keepNodeId && nodes.value.some((node) => node.id === keepNodeId)) {
      selectedNodeId.value = keepNodeId;
      fillNodeForm(selectedNode.value);
    } else {
      selectedNodeId.value = null;
      resetNodeForm();
    }
  }

  async function loadMaps() {
    const data = await run(() => api('/api/mind-maps'), '导图已刷新');
    if (!data) {
      return;
    }

    maps.value = data;
    if (maps.value.length === 0) {
      selectedMapId.value = null;
      mapTitle.value = '';
      nodes.value = [];
      relations.value = [];
      selectedNodeId.value = null;
      resetNodeForm();
      return;
    }

    const nextMap = maps.value.find((map) => map.id === selectedMapId.value) ?? maps.value[0];
    await selectMap(nextMap.id);
  }

  async function refreshMapList() {
    const data = await run(() => api('/api/mind-maps'), '思维导图列表已刷新');
    if (!data) {
      return;
    }

    maps.value = data;
    if (maps.value.length === 0) {
      selectedMapId.value = null;
      mapTitle.value = '';
      nodes.value = [];
      relations.value = [];
      selectedNodeId.value = null;
      resetNodeForm();
      return;
    }

    const activeMap = maps.value.find((map) => map.id === selectedMapId.value);
    if (activeMap) {
      mapTitle.value = activeMap.title;
      return;
    }

    await selectMap(maps.value[0].id);
  }

  async function loadAiModels() {
    // 从全局模型管理器中同步模型列表和选中项
    const data = await run(() => api('/api/ai/models'));
    if (data) {
      aiModels.value = data;
    }

    // 合并自定义模型
    try {
      const customRaw = localStorage.getItem('netmind_custom_models');
      const custom = customRaw ? JSON.parse(customRaw) : [];
      aiModels.value = [...aiModels.value, ...custom];
    } catch { /* 忽略 */ }

    // 从全局选择中同步
    selectedAiModelId.value = globalSelectedId.value
      || aiModels.value.find((model) => model.isDefault)?.id
      || aiModels.value[0]?.id
      || '';
  }

  async function selectMap(id) {
    selectedMapId.value = id;
    const map = maps.value.find((item) => item.id === id);
    mapTitle.value = map?.title ?? '';
    await refreshMapData(id, { keepNodeId: null, message: '导图已加载' });
  }

  async function refreshSelectedMapData(message = '导图内容已刷新') {
    if (!selectedMap.value) {
      showToast('error', '请先选择思维导图');
      return;
    }

    await refreshMapData(selectedMap.value.id, {
      keepNodeId: selectedNodeId.value,
      message
    });
  }

  async function createMap() {
    const title = mapTitle.value.trim();
    if (!title) {
      showToast('error', '请输入导图标题');
      return null;
    }

    const created = await run(
      () => api('/api/mind-maps', { method: 'POST', body: JSON.stringify({ title }) }),
      '导图已创建'
    );

    if (created) {
      await loadMaps();
      await selectMap(created.id);
    }

    return created;
  }

  function selectNode(id) {
    selectedNodeId.value = id;
    fillNodeForm(selectedNode.value);
  }

  async function createNode(parentId = null) {
    if (!selectedMap.value) {
      showToast('error', '请先选择导图');
      return;
    }

    const title = nodeForm.value.title.trim();
    if (!title) {
      showToast('error', '请输入节点标题');
      return;
    }

    const created = await run(
      () => api('/api/nodes', {
        method: 'POST',
        body: JSON.stringify({
          mapId: selectedMap.value.id,
          parentId,
          title,
          content: nodeForm.value.content,
          orderNo: Number(nodeForm.value.orderNo) || 0,
          positionX: null,
          positionY: null
        })
      }),
      '节点已创建'
    );

    if (created) {
      await refreshMapData(selectedMap.value.id, { keepNodeId: created.id });
    }
  }

  async function createCanvasNode(payload = {}) {
    const previousForm = { ...nodeForm.value };
    nodeForm.value = {
      title: payload.title ?? '新节点',
      content: payload.content ?? '',
      orderNo: payload.orderNo ?? (nodes.value.length + 1)
    };
    await createNode(payload.parentId ?? null);
    if (!selectedNode.value) {
      nodeForm.value = previousForm;
    }
  }

  async function updateNode() {
    if (!selectedNode.value) {
      showToast('error', '请先选择节点');
      return;
    }

    const title = nodeForm.value.title.trim();
    if (!title) {
      showToast('error', '请输入节点标题');
      return;
    }

    const updated = await run(
      () => api(`/api/nodes/${selectedNode.value.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          parentId: selectedNode.value.parentId,
          title,
          content: nodeForm.value.content,
          orderNo: Number(nodeForm.value.orderNo) || 0,
          positionX: selectedNode.value.positionX ?? null,
          positionY: selectedNode.value.positionY ?? null
        })
      }),
      '节点已保存'
    );

    if (updated) {
      await refreshMapData(selectedMap.value.id, { keepNodeId: updated.id });
    }
  }

  async function updateCanvasNode(payload = {}) {
    if (!selectedNode.value) {
      showToast('error', '请先选择节点');
      return;
    }

    nodeForm.value = {
      title: payload.title ?? selectedNode.value.title,
      content: payload.content ?? selectedNode.value.content ?? '',
      orderNo: payload.orderNo ?? selectedNode.value.orderNo
    };
    if (Object.prototype.hasOwnProperty.call(payload, 'positionX')) {
      selectedNode.value.positionX = payload.positionX;
    }
    if (Object.prototype.hasOwnProperty.call(payload, 'positionY')) {
      selectedNode.value.positionY = payload.positionY;
    }
    await updateNode();
  }

  async function saveCanvasNodePositions(positionUpdates = []) {
    if (!selectedMap.value || positionUpdates.length === 0) {
      return;
    }

    const updatesById = new Map(positionUpdates.map((item) => [item.nodeId, item]));
    const targets = nodes.value.filter((node) => updatesById.has(node.id));
    if (targets.length === 0) {
      showToast('error', '没有可保存的位置变更');
      return;
    }

    const saved = await run(
      () => Promise.all(targets.map((node) => {
        const position = updatesById.get(node.id);
        return api(`/api/nodes/${node.id}`, {
          method: 'PUT',
          body: JSON.stringify({
            parentId: node.parentId,
            title: node.title,
            content: node.content,
            orderNo: Number(node.orderNo) || 0,
            positionX: position.positionX,
            positionY: position.positionY
          })
        });
      })),
      `已保存 ${targets.length} 个节点位置`
    );

    if (saved) {
      await refreshMapData(selectedMap.value.id, { keepNodeId: selectedNodeId.value });
    }
  }

  async function deleteSelectedMap() {
    if (!selectedMap.value) {
      showToast('error', '请先选择思维导图');
      return;
    }

    const deletedMapId = selectedMap.value.id;
    const deleted = await run(
      () => api(`/api/mind-maps/${deletedMapId}/cascade`, { method: 'DELETE' }),
      '思维导图已删除'
    );

    if (deleted) {
      if (selectedMapId.value === deletedMapId) {
        selectedMapId.value = null;
      }
      await loadMaps();
    }
  }

  async function deleteNode(subtree) {
    if (!selectedNode.value) {
      showToast('error', '请先选择节点');
      return;
    }

    const deletedNodeId = selectedNode.value.id;
    const deleted = await run(
      () => api(`/api/nodes/${deletedNodeId}${subtree ? '/subtree' : ''}`, { method: 'DELETE' }),
      subtree ? '节点子树已删除' : '节点已删除'
    );

    if (deleted) {
      await refreshMapData(selectedMap.value.id, { keepNodeId: null });
    }
  }

  async function createRelation() {
    if (!selectedMap.value || !selectedNode.value) {
      showToast('error', '请先选择导图和源节点');
      return;
    }

    const targetId = Number(relationForm.value.targetId);
    if (!targetId) {
      showToast('error', '请选择目标节点');
      return;
    }

    const created = await run(
      () => api('/api/node-relations', {
        method: 'POST',
        body: JSON.stringify({
          mapId: selectedMap.value.id,
          sourceId: selectedNode.value.id,
          targetId,
          relationType: relationForm.value.relationType.trim() || 'relates_to',
          weight: Number(relationForm.value.weight) || 0
        })
      }),
      '关联已创建'
    );

    if (created) {
      relationForm.value = { targetId: '', relationType: 'relates_to', weight: 1 };
      await refreshMapData(selectedMap.value.id, { keepNodeId: selectedNodeId.value });
    }
  }

  async function deleteRelation(id) {
    const deleted = await run(() => api(`/api/node-relations/${id}`, { method: 'DELETE' }), '关联已删除');
    if (deleted) {
      await refreshMapData(selectedMap.value.id, { keepNodeId: selectedNodeId.value });
    }
  }

  async function jumpToNode({ mapId, nodeId }) {
    if (selectedMapId.value === mapId) {
      selectedNodeId.value = nodeId;
      const node = nodes.value.find(n => n.id === nodeId);
      if (node) fillNodeForm(node);
    } else {
      selectedMapId.value = mapId;
      let map = maps.value.find(m => m.id === mapId);

      if (!map) {
        const data = await run(() => api('/api/mind-maps'));
        if (data) {
          maps.value = data;
          map = maps.value.find(m => m.id === mapId);
        }
      }

      if (map) {
        mapTitle.value = map.title;
        await refreshMapData(mapId, { keepNodeId: nodeId, message: `已切换至导图：${map.title}` });
        const node = nodes.value.find(n => n.id === nodeId);
        if (node) fillNodeForm(node);
      } else {
        showToast('error', '无法找到目标思维导图');
      }
    }
  }

  async function searchNodes(keyword, limit = 10, crossMap = true) {
    if (!keyword) {
      return [];
    }
    const mapIdParam = crossMap ? '' : `&mapId=${selectedMapId.value}`;
    return run(() => api(`/api/nodes/search?keyword=${encodeURIComponent(keyword)}&limit=${limit}${mapIdParam}`));
  }

  function exportStructure() {
    if (!selectedMap.value) {
      showToast('error', '请先选择导图');
      return;
    }

    return run(async () => {
      const structure = await api(`/api/mind-map-transfer/${selectedMap.value.id}/structure`);
      transferText.value = JSON.stringify(structure.transfer, null, 2);
      return structure;
    }, '结构体已导出');
  }

  function downloadSelectedMap() {
    if (!selectedMap.value) {
      showToast('error', '请先选择导图');
      return;
    }

    downloadUrl(`/api/mind-map-transfer/${selectedMap.value.id}/file`);
  }

  async function importStructure() {
    const raw = transferText.value.trim();
    if (!raw) {
      showToast('error', '请先粘贴导图结构体');
      return;
    }

    let parsed;
    try {
      parsed = JSON.parse(raw);
    } catch {
      showToast('error', '导图结构 JSON 格式无效');
      return;
    }

    const imported = await run(
      () => api('/api/mind-map-transfer/structure', {
        method: 'POST',
        body: JSON.stringify({
          mindMap: parsed.mindMap ?? parsed,
          titleOverride: importTitleOverride.value.trim() || null
        })
      }),
      '结构体已导入'
    );

    if (imported) {
      await loadMaps();
      await selectMap(imported.structure.map.id);
    }
  }

  async function importFile(event) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    const form = new FormData();
    form.append('file', file);
    if (importTitleOverride.value.trim()) {
      form.append('titleOverride', importTitleOverride.value.trim());
    }

    const imported = await run(
      () => api('/api/mind-map-transfer/file', { method: 'POST', body: form }),
      '文件已导入'
    );

    if (fileInput.value) {
      fileInput.value.value = '';
    }

    if (imported) {
      await loadMaps();
      await selectMap(imported.structure.map.id);
    }
  }

  async function cleanNaturalLanguage() {
    const naturalLanguage = naturalLanguageInput.value.trim();
    if (!naturalLanguage) {
      showToast('error', '请输入自然语言内容');
      return;
    }

    const modelConfig = getGlobalModelConfig();
    if (!modelConfig.modelId) {
      showToast('error', '未选择 AI 模型。请在「设置 → 全局默认 AI 模型」中选择。');
      return;
    }

    transferText.value = '';
    aiStatus.value = 'AI 正在清洗文本...';
    const result = await run(
      () => api('/api/ai/clean', {
        method: 'POST',
        body: JSON.stringify({
          naturalLanguage,
          modelId: modelConfig.modelId,
          endpoint: modelConfig.endpoint || null,
          provider: modelConfig.provider || null,
          apiKey: modelConfig.apiKey || null
        })
      }),
      'AI 结构体已生成'
    );

    if (result) {
      transferText.value = JSON.stringify(result.transfer, null, 2);
      aiStatus.value = 'AI 结构体已生成';
    } else {
      aiStatus.value = '';
    }
  }

  function buildConversationContext(messages = chatMessages.value) {
    return messages
      .map((message) => `${message.role === 'user' ? '用户' : 'AI'}：${message.content}`)
      .join('\n\n');
  }

  async function sendChatMessage() {
    const message = chatInput.value.trim();
    if (!message) {
      showToast('error', '请输入对话内容');
      return;
    }

    const modelConfig = getGlobalModelConfig();

    const previousContext = buildConversationContext();
    chatMessages.value.push({ role: 'user', content: message });
    chatInput.value = '';
    aiStatus.value = 'AI 正在回复...';

    const result = await run(
      () => api('/api/ai/context-chat', {
        method: 'POST',
        body: JSON.stringify({
          message,
          conversationId: chatConversationId.value,
          context: previousContext,
          modelId: modelConfig.modelId || null,
          endpoint: modelConfig.endpoint || null,
          provider: modelConfig.provider || null,
          apiKey: modelConfig.apiKey || null
        })
      }),
      'AI 已回复'
    );

    if (result) {
      chatMessages.value.push({ role: 'assistant', content: result.reply });
      aiStatus.value = result.wasContextCompressed
        ? 'AI 已回复，较长对话上下文已先压缩'
        : 'AI 已回复';
    } else {
      aiStatus.value = '';
    }
  }

  function startNewConversation() {
    chatMessages.value = [];
    chatInput.value = '';
    chatConversationId.value = createConversationId();
    aiStatus.value = '已开始新对话';
  }

  async function loadConversationHistory() {
    chatHistoryOpen.value = true;
    chatHistoryLoading.value = true;
    try {
      const records = await api('/api/ai-conversation-records');
      const groups = new Map();
      records.forEach((record) => {
        if (!groups.has(record.conversationId)) {
          groups.set(record.conversationId, []);
        }
        groups.get(record.conversationId).push(record);
      });

      chatHistoryGroups.value = [...groups.entries()]
        .map(([conversationId, items]) => {
          const ordered = [...items].sort((left, right) => new Date(left.createdAt) - new Date(right.createdAt));
          const firstUser = ordered.find((item) => item.role === 'user');
          const last = ordered[ordered.length - 1];
          return {
            conversationId,
            records: ordered,
            title: firstUser?.content?.slice(0, 36) || '未命名对话',
            updatedAt: last?.updatedAt ?? last?.createdAt ?? '',
            count: ordered.length
          };
        })
        .sort((left, right) => new Date(right.updatedAt) - new Date(left.updatedAt));
    } catch (error) {
      showToast('error', error instanceof Error ? error.message : '历史对话加载失败');
    } finally {
      chatHistoryLoading.value = false;
    }
  }

  function restoreConversation(group) {
    chatConversationId.value = group.conversationId;
    chatMessages.value = group.records.map((record) => ({
      role: record.role,
      content: record.content
    }));
    chatHistoryOpen.value = false;
    chatOpen.value = true;
    aiStatus.value = '已载入历史对话';
  }

  async function cleanConversationContext() {
    const context = chatContextText.value.trim();
    if (!context) {
      showToast('error', '请先开始一轮对话');
      return;
    }

    const modelConfig = getGlobalModelConfig();

    transferText.value = '';
    aiStatus.value = 'AI 正在根据本次对话生成结构体...';
    const result = await run(
      () => api('/api/ai/clean', {
        method: 'POST',
        body: JSON.stringify({
          naturalLanguage: context,
          modelId: modelConfig.modelId || null,
          endpoint: modelConfig.endpoint || null,
          provider: modelConfig.provider || null,
          apiKey: modelConfig.apiKey || null
        })
      }),
      '本次对话已生成结构体'
    );

    if (result) {
      transferText.value = JSON.stringify(result.transfer, null, 2);
      aiStatus.value = '本次对话已生成结构体';
    } else {
      aiStatus.value = '';
    }
  }

  return {
    maps,
    nodes,
    relations,
    selectedMapId,
    selectedNodeId,
    selectedMap,
    selectedNode,
    candidateTargets,
    selectedNodeRelations,
    nodeTitleById,
    mapTitle,
    nodeForm,
    relationForm,
    transferText,
    importTitleOverride,
    fileInput,
    aiModels,
    selectedAiModelId,
    naturalLanguageInput,
    aiStatus,
    chatOpen,
    chatInput,
    chatMessages,
    chatConversationId,
    chatHistoryOpen,
    chatHistoryLoading,
    chatHistoryGroups,
    loading,
    toast,
    visualNodes,
    loadMaps,
    refreshMapList,
    loadAiModels,
    selectMap,
    refreshSelectedMapData,
    createMap,
    selectNode,
    createNode,
    createCanvasNode,
    updateNode,
    updateCanvasNode,
    saveCanvasNodePositions,
    deleteSelectedMap,
    deleteNode,
    createRelation,
    deleteRelation,
    jumpToNode,
    searchNodes,
    exportStructure,
    downloadSelectedMap,
    downloadUrl,
    importStructure,
    importFile,
    cleanNaturalLanguage,
    sendChatMessage,
    startNewConversation,
    loadConversationHistory,
    restoreConversation,
    cleanConversationContext
  };
}
