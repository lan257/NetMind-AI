<script setup>
import { onMounted, ref } from 'vue';
import AppHeader from './components/AppHeader.vue';
import CreateMapPage from './components/CreateMapPage.vue';
import FloatingMessage from './components/FloatingMessage.vue';
import MapSidebar from './components/MapSidebar.vue';
import MindMapCanvas from './components/MindMapCanvas.vue';
import NodePreviewDialog from './components/NodePreviewDialog.vue';
import NodeTreeView from './components/NodeTreeView.vue';
import ViewSwitcher from './components/ViewSwitcher.vue';
import WorkbenchInspector from './components/WorkbenchInspector.vue';
import { useMindMapWorkspace } from './composables/useMindMapWorkspace';

const workspace = useMindMapWorkspace();
const page = ref('main');
const workMode = ref('display');
const viewMode = ref('graph');
const createViewMode = ref('graph');
const previewOpen = ref(false);
const previewNode = ref(null);

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
  previewOpen.value = true;
}

onMounted(async () => {
  await Promise.all([workspace.loadMaps(), workspace.loadAiModels()]);
});
</script>

<template>
  <main class="workspace">
    <AppHeader
      :page="page"
      :search-nodes="workspace.searchNodes"
      @go-main="openMainPage"
      @jump-to-node="workspace.jumpToNode"
    />
    <FloatingMessage :toast="workspace.toast.value" />

    <template v-if="page === 'main'">
      <ViewSwitcher v-model:work-mode="workMode" v-model:view-mode="viewMode" />
      <section class="layout" :class="{ 'with-inspector': workMode === 'workbench' && viewMode === 'list' }">
        <MapSidebar
          :maps="workspace.maps.value"
          :selected-map-id="workspace.selectedMapId.value"
          :loading="workspace.loading.value"
          :deletable="workMode === 'workbench'"
          @select-map="workspace.selectMap"
          @create-map="openCreatePage"
          @delete-map="workspace.deleteSelectedMap"
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
          @select-node="workspace.selectNode"
          @preview-node="preview"
          @create-node="workspace.createCanvasNode"
          @update-node="workspace.updateCanvasNode"
          @save-node-positions="workspace.saveCanvasNodePositions"
          @delete-node="workspace.deleteNode(false)"
        />
        <NodeTreeView
          v-else
          :map="workspace.selectedMap.value"
          :nodes="workspace.nodes.value"
          :selected-node-id="workspace.selectedNodeId.value"
          :preview-on-click="workMode !== 'workbench'"
          @select-node="workspace.selectNode"
          @preview-node="preview"
        />
        <WorkbenchInspector
          v-if="workMode === 'workbench' && viewMode === 'list'"
          :selected-map="workspace.selectedMap.value"
          :selected-node="workspace.selectedNode.value"
          :node-form="workspace.nodeForm.value"
          :relation-form="workspace.relationForm.value"
          :candidate-targets="workspace.candidateTargets.value"
          :selected-node-relations="workspace.selectedNodeRelations.value"
          :node-title-by-id="workspace.nodeTitleById.value"
          :loading="workspace.loading.value"
          :search-nodes="workspace.searchNodes"
          @create-root="workspace.createNode(null)"
          @create-child="workspace.createNode(workspace.selectedNode.value?.id ?? null)"
          @save-node="workspace.updateNode"
          @delete-node="workspace.deleteNode(false)"
          @delete-subtree="workspace.deleteNode(true)"
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

    <NodePreviewDialog
      v-model="previewOpen"
      :node="previewNode"
      :nodes="workspace.nodes.value"
      :relations="workspace.relations.value"
      :current-map-id="workspace.selectedMap.value?.id"
      @preview-node="preview"
      @jump-to-node="workspace.jumpToNode"
    />
  </main>
</template>
