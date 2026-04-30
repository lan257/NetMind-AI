import { computed, ref } from 'vue';
import { api, downloadUrl } from '../services/api';

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
  const loading = ref(false);
  const toast = ref({ type: '', text: '' });

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

  async function loadAiModels() {
    const data = await run(() => api('/api/ai/models'));
    if (!data) {
      return;
    }

    aiModels.value = data;
    selectedAiModelId.value = data.find((model) => model.isDefault)?.id ?? data[0]?.id ?? '';
  }

  async function selectMap(id) {
    selectedMapId.value = id;
    const map = maps.value.find((item) => item.id === id);
    mapTitle.value = map?.title ?? '';
    await refreshMapData(id, { keepNodeId: null, message: '导图已加载' });
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
          orderNo: Number(nodeForm.value.orderNo) || 0
        })
      }),
      '节点已创建'
    );

    if (created) {
      await refreshMapData(selectedMap.value.id, { keepNodeId: created.id });
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
          orderNo: Number(nodeForm.value.orderNo) || 0
        })
      }),
      '节点已保存'
    );

    if (updated) {
      await refreshMapData(selectedMap.value.id, { keepNodeId: updated.id });
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

    transferText.value = '';
    aiStatus.value = 'AI 正在清洗文本...';
    const result = await run(
      () => api('/api/ai/clean', {
        method: 'POST',
        body: JSON.stringify({
          naturalLanguage,
          modelId: selectedAiModelId.value || null
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
          modelId: selectedAiModelId.value || null
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

  async function cleanConversationContext() {
    const context = chatContextText.value.trim();
    if (!context) {
      showToast('error', '请先开始一轮对话');
      return;
    }

    transferText.value = '';
    aiStatus.value = 'AI 正在根据本次对话生成结构体...';
    const result = await run(
      () => api('/api/ai/clean', {
        method: 'POST',
        body: JSON.stringify({
          naturalLanguage: context,
          modelId: selectedAiModelId.value || null
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
    loading,
    toast,
    visualNodes,
    loadMaps,
    loadAiModels,
    selectMap,
    createMap,
    selectNode,
    createNode,
    updateNode,
    deleteNode,
    createRelation,
    deleteRelation,
    exportStructure,
    downloadSelectedMap,
    downloadUrl,
    importStructure,
    importFile,
    cleanNaturalLanguage,
    sendChatMessage,
    startNewConversation,
    cleanConversationContext
  };
}
