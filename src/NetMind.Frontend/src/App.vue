<script setup>
import { onMounted, ref } from 'vue';
import AppHeader from './components/AppHeader.vue';
import CreateMapPage from './components/CreateMapPage.vue';
import FloatingMessage from './components/FloatingMessage.vue';
import MapSidebar from './components/MapSidebar.vue';
import MindMapCanvas from './components/MindMapCanvas.vue';
import KnowledgeCard from './components/KnowledgeCard.vue';
import NodeTreeView from './components/NodeTreeView.vue';
import SettingsDialog from './components/SettingsDialog.vue';
import { useMindMapWorkspace } from './composables/useMindMapWorkspace';
import { loadGlobalModels } from './composables/useGlobalModel';

const workspace = useMindMapWorkspace();
const page = ref('main');
const workMode = ref('display');
const viewMode = ref('graph');
const createViewMode = ref('graph');
const previewNode = ref(null);
const settingsOpen = ref(false);

function openCreatePage() {
  page.value = 'create';
}

function openMainPage() {
  page.value = 'main';
}

async function createMapAndReturn() {
  const created = await workspace.createMap();
  if (created) {
    page.value = 'main';
  }
}

function preview(node) {
  previewNode.value = node;
}

function openSettings() {
  settingsOpen.value = true;
}

onMounted(async () => {
  await Promise.all([workspace.loadMaps(), loadGlobalModels()]);
  await workspace.loadAiModels();
});
</script>

<template>
  <main class="workspace">
    <AppHeader
      :page="page"
      :search-nodes="workspace.searchNodes"
      :work-mode="workMode"
      :view-mode="viewMode"
      @go-main="openMainPage"
      @jump-to-node="workspace.jumpToNode"
      @open-settings="openSettings"
      @update:work-mode="workMode = $event"
      @update:view-mode="viewMode = $event"
    />
    <FloatingMessage :toast="workspace.toast.value" />

    <template v-if="page === 'main'">
      <section class="layout" :class="{ 'with-knowledge-card': workMode === 'display' || workMode === 'workbench' }">
        <MapSidebar
          :maps="workspace.maps.value"
          :selected-map-id="workspace.selectedMapId.value"
          :loading="workspace.loading.value"
          :deletable="workMode === 'workbench'"
          @select-map="workspace.selectMap"
          @create-map="openCreatePage"
          @delete-map="workspace.deleteSelectedMap"
          @refresh-maps="workspace.refreshMapList"
        />
        <MindMapCanvas
          v-if="viewMode === 'graph'"
          :map="workspace.selectedMap.value"
          :nodes="workspace.nodes.value"
          :relations="workspace.relations.value"
          :selected-node-id="workspace.selectedNodeId.value"
          :editable="workMode === 'workbench'"
          :loading="workspace.loading.value"
          :preview-on-click="workMode !== 'workbench'"
          :search-nodes="workspace.searchNodes"
          :hide-canvas-editor="workMode === 'workbench'"
          @select-node="workspace.selectNode"
          @preview-node="preview"
          @create-node="workspace.createCanvasNode"
          @update-node="workspace.updateCanvasNode"
          @save-node-positions="workspace.saveCanvasNodePositions"
          @delete-node="workspace.deleteNode(true)"
          @refresh-map="workspace.refreshSelectedMapData('画布已重置')"
        />
        <NodeTreeView
          v-else
          :map="workspace.selectedMap.value"
          :nodes="workspace.nodes.value"
          :selected-node-id="workspace.selectedNodeId.value"
          :preview-on-click="workMode !== 'workbench'"
          :editable="workMode === 'workbench'"
          :loading="workspace.loading.value"
          :selected-node="workspace.selectedNode.value"
          @select-node="workspace.selectNode"
          @preview-node="preview"
          @create-root="workspace.createNode(null)"
          @create-child="workspace.createNode(workspace.selectedNode.value?.id ?? null)"
          @delete-node="workspace.deleteNode(true)"
          @refresh-nodes="workspace.refreshSelectedMapData('节点列表已刷新')"
        />
        <KnowledgeCard
          :node="workMode === 'workbench' ? workspace.selectedNode.value : previewNode"
          :nodes="workspace.nodes.value"
          :relations="workspace.relations.value"
          :current-map-id="workspace.selectedMap.value?.id"
          :work-mode="workMode"
          :node-form="workspace.nodeForm.value"
          :relation-form="workspace.relationForm.value"
          :candidate-targets="workspace.candidateTargets.value"
          :selected-node-relations="workspace.selectedNodeRelations.value"
          :node-title-by-id="workspace.nodeTitleById.value"
          :loading="workspace.loading.value"
          :search-nodes="workspace.searchNodes"
          @preview-node="preview"
          @jump-to-node="workspace.jumpToNode"
          @save-node="workspace.updateNode"
          @create-relation="workspace.createRelation"
          @delete-relation="workspace.deleteRelation"
        />
      </section>
    </template>

    <CreateMapPage
      v-else
      :workspace="workspace"
      :view-mode="createViewMode"
      @created="createMapAndReturn"
      @update:view-mode="createViewMode = $event"
      @preview-node="preview"
    />

    <SettingsDialog v-model="settingsOpen" />
  </main>
</template>
