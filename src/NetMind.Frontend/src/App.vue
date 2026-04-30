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
    <AppHeader :page="page" @go-main="openMainPage" />
    <FloatingMessage :toast="workspace.toast.value" />

    <template v-if="page === 'main'">
      <ViewSwitcher v-model:work-mode="workMode" v-model:view-mode="viewMode" />
      <section class="layout" :class="{ 'with-inspector': workMode === 'workbench' && viewMode === 'list' }">
        <MapSidebar
          :maps="workspace.maps.value"
          :selected-map-id="workspace.selectedMapId.value"
          :loading="workspace.loading.value"
          @select-map="workspace.selectMap"
          @create-map="openCreatePage"
        />
        <MindMapCanvas
          v-if="viewMode === 'graph'"
          :map="workspace.selectedMap.value"
          :nodes="workspace.nodes.value"
          :selected-node-id="workspace.selectedNodeId.value"
          :preview-on-click="workMode !== 'workbench'"
          @select-node="workspace.selectNode"
          @preview-node="preview"
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

    <NodePreviewDialog v-model="previewOpen" :node="previewNode" />
  </main>
</template>
