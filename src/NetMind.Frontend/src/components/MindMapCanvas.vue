<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { Delete, EditPen, Plus, Refresh, ZoomIn, ZoomOut, Link } from '@element-plus/icons-vue';
import { renderMarkdown } from '../composables/useMarkdown';

const props = defineProps({
  map: { type: Object, default: null },
  nodes: { type: Array, default: () => [] },
  relations: { type: Array, default: () => [] },
  selectedNodeId: { type: [Number, String, null], default: null },
  previewOnClick: { type: Boolean, default: true },
  editable: { type: Boolean, default: false },
  loading: { type: Boolean, default: false },
  searchNodes: { type: Function, default: null },
  hideCanvasEditor: { type: Boolean, default: false }
});

const emit = defineEmits(['select-node', 'preview-node', 'create-node', 'update-node', 'delete-node', 'refresh-map']);

const canvasRef = ref(null);
const wrapRef = ref(null);
const hoverNodeId = ref(null);
const hitRegions = ref([]);
const actionRegions = ref([]);
const collapsedNodeIds = ref(new Set());
const viewport = ref({ x: 0, y: 0, scale: 1 });
const editorForm = ref({ title: '', content: '', orderNo: 1 });
let resizeObserver = null;
let rafId = 0;
let interaction = null;

const searchKeyword = ref('');
const searchResults = ref([]);
const searching = ref(false);
const showRefDialog = ref(false);
const refTriggerPos = ref({ start: -1, end: -1 });

async function handleSearch(query) {
  if (!query) {
    searchResults.value = [];
    return;
  }
  searching.value = true;
  try {
    const results = await props.searchNodes(query);
    // 过滤当前节点
    searchResults.value = results.filter(n => n.id !== props.selectedNodeId);
  } finally {
    searching.value = false;
  }
}

function insertReference(node) {
  const refText = `[[${node.title}|${node.id}]]`;
  const content = editorForm.value.content || '';
  
  // 如果是因为输入 [[ 触发的，尝试替换最后的 [[
  if (refTriggerPos.value.start >= 0) {
    const before = content.slice(0, refTriggerPos.value.start);
    const after = content.slice(refTriggerPos.value.end);
    editorForm.value.content = before + refText + after;
  } else {
    editorForm.value.content = content + (content.length > 0 && !content.endsWith('\n') ? '\n' : '') + refText;
  }
  
  showRefDialog.value = false;
  searchKeyword.value = '';
  searchResults.value = [];
  refTriggerPos.value = { start: -1, end: -1 };
}

function onContentKeyup(event) {
  const el = event.target;
  if (!el) return;
  const pos = el.selectionStart;
  const text = el.value || '';
  if (pos >= 2 && text.slice(pos - 2, pos) === '[[') {
    refTriggerPos.value = { start: pos - 2, end: pos };
    showRefDialog.value = true;
  }
}

function onRefDialogOpened() {
  nextTick(() => {
    const input = document.querySelector('.ref-dialog-select-wrap .el-select__input');
    if (input) input.focus();
  });
}

// [[ 触发现在通过 onContentKeyup 在 keyup 事件中处理

const selectedNode = computed(() => props.nodes.find((node) => node.id === props.selectedNodeId) ?? null);
const orderedNodes = computed(() => [...props.nodes].sort((left, right) => {
  return (left.orderNo ?? 0) - (right.orderNo ?? 0) || left.id - right.id;
}));

function scheduleDraw() {
  window.cancelAnimationFrame(rafId);
  rafId = window.requestAnimationFrame(() => drawCanvas());
}

function getChildrenByParent() {
  const childrenByParent = new Map();
  orderedNodes.value.forEach((node) => {
    const parentKey = node.parentId ?? 0;
    if (!childrenByParent.has(parentKey)) {
      childrenByParent.set(parentKey, []);
    }
    childrenByParent.get(parentKey).push(node);
  });
  return childrenByParent;
}

function countSubtreeLeaves(node, childrenByParent) {
  const children = childrenByParent.get(node.id) ?? [];
  if (children.length === 0 || collapsedNodeIds.value.has(node.id)) {
    return 1;
  }
  return children.reduce((total, child) => total + countSubtreeLeaves(child, childrenByParent), 0);
}

function wrapText(ctx, text, maxWidth) {
  const chars = String(text || '未命名节点').split('');
  const lines = [];
  let current = '';

  chars.forEach((char) => {
    const next = `${current}${char}`;
    if (ctx.measureText(next).width > maxWidth && current) {
      lines.push(current);
      current = char;
    } else {
      current = next;
    }
  });

  if (current) {
    lines.push(current);
  }
  return lines.slice(0, 3);
}

function createLayout(ctx) {
  const childrenByParent = getChildrenByParent();
  const root = {
    id: 'map-root',
    title: props.map?.title ?? '思维导图',
    x: 0,
    y: 0,
    width: 190,
    height: 64,
    lines: wrapText(ctx, props.map?.title ?? '思维导图', 150),
    isRoot: true
  };
  const layoutNodes = [root];
  const links = [];
  const rootGap = 220;
  const levelGap = 210;
  const leafGap = 92;
  const sides = [
    { direction: 1, nodes: [] },
    { direction: -1, nodes: [] }
  ];

  (childrenByParent.get(0) ?? []).forEach((node, index) => {
    sides[index % 2].nodes.push(node);
  });

  const placeNode = (node, direction, depth, y) => {
    const lines = wrapText(ctx, node.title, 144);
    const widthValue = Math.max(132, Math.min(196, Math.max(...lines.map((line) => ctx.measureText(line).width)) + 34));
    const heightValue = Math.max(48, lines.length * 18 + 24);
    const graphNode = {
      ...node,
      x: direction * (rootGap + (depth - 1) * levelGap),
      y,
      width: widthValue,
      height: heightValue,
      lines,
      direction,
      childCount: (childrenByParent.get(node.id) ?? []).length,
      collapsed: collapsedNodeIds.value.has(node.id)
    };
    layoutNodes.push(graphNode);
    return graphNode;
  };

  sides.forEach((side) => {
    const totalLeaves = side.nodes.reduce((total, node) => total + countSubtreeLeaves(node, childrenByParent), 0);
    let cursor = -((Math.max(totalLeaves, 1) - 1) * leafGap) / 2;

    const walk = (node, parent, depth) => {
      const allChildren = childrenByParent.get(node.id) ?? [];
      const children = collapsedNodeIds.value.has(node.id) ? [] : allChildren;
      const leafCount = countSubtreeLeaves(node, childrenByParent);
      const startY = cursor;
      const endY = cursor + Math.max(leafCount - 1, 0) * leafGap;
      const y = (startY + endY) / 2;
      const placed = placeNode(node, side.direction, depth, y);
      links.push({ from: parent, to: placed, direction: side.direction });

      if (children.length === 0) {
        cursor += leafGap;
        return;
      }
      children.forEach((child) => walk(child, placed, depth + 1));
    };

    side.nodes.forEach((node) => walk(node, root, 1));
  });

  return { layoutNodes, links };
}

function getBounds(layoutNodes) {
  return layoutNodes.reduce((box, node) => ({
    minX: Math.min(box.minX, node.x - node.width / 2),
    maxX: Math.max(box.maxX, node.x + node.width / 2),
    minY: Math.min(box.minY, node.y - node.height / 2),
    maxY: Math.max(box.maxY, node.y + node.height / 2)
  }), { minX: 0, maxX: 0, minY: 0, maxY: 0 });
}

function fitView() {
  const canvas = canvasRef.value;
  const wrapper = wrapRef.value;
  if (!canvas || !wrapper) {
    return;
  }
  const ctx = canvas.getContext('2d');
  ctx.font = '14px "Microsoft YaHei", "Segoe UI", Arial';
  const layout = createLayout(ctx);
  const bounds = getBounds(layout.layoutNodes);
  const width = Math.max(320, Math.floor(wrapper.clientWidth));
  const height = Math.max(320, Math.floor(wrapper.clientHeight));
  const graphWidth = bounds.maxX - bounds.minX + 140;
  const graphHeight = bounds.maxY - bounds.minY + 140;
  const scale = Math.min(1, width / graphWidth, height / graphHeight);
  viewport.value = {
    x: -((bounds.minX + bounds.maxX) / 2) * scale,
    y: -((bounds.minY + bounds.maxY) / 2) * scale,
    scale
  };
  scheduleDraw();
}

function toScreen(point, width, height) {
  return {
    x: width / 2 + viewport.value.x + point.x * viewport.value.scale,
    y: height / 2 + viewport.value.y + point.y * viewport.value.scale
  };
}

function drawGrid(ctx, width, height) {
  ctx.fillStyle = '#fbfdff';
  ctx.fillRect(0, 0, width, height);
  ctx.strokeStyle = 'rgba(64, 158, 255, 0.08)';
  ctx.lineWidth = 1;
  const grid = Math.max(14, 28 * viewport.value.scale);
  const offsetX = viewport.value.x % grid;
  const offsetY = viewport.value.y % grid;

  for (let x = offsetX; x < width; x += grid) {
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, height);
    ctx.stroke();
  }
  for (let y = offsetY; y < height; y += grid) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(width, y);
    ctx.stroke();
  }
}

function roundedRect(ctx, x, y, width, height, radius) {
  const safeRadius = Math.min(radius, width / 2, height / 2);
  ctx.beginPath();
  ctx.moveTo(x + safeRadius, y);
  ctx.arcTo(x + width, y, x + width, y + height, safeRadius);
  ctx.arcTo(x + width, y + height, x, y + height, safeRadius);
  ctx.arcTo(x, y + height, x, y, safeRadius);
  ctx.arcTo(x, y, x + width, y, safeRadius);
  ctx.closePath();
}

function drawLink(ctx, from, to, direction, width, height) {
  const source = toScreen(from, width, height);
  const target = toScreen(to, width, height);
  const sourceWidth = from.width * viewport.value.scale;
  const targetWidth = to.width * viewport.value.scale;
  const startX = source.x + (direction * sourceWidth) / 2;
  const endX = target.x - (direction * targetWidth) / 2;
  const controlGap = Math.max(70 * viewport.value.scale, Math.abs(endX - startX) / 2);

  ctx.beginPath();
  ctx.moveTo(startX, source.y);
  ctx.bezierCurveTo(startX + direction * controlGap, source.y, endX - direction * controlGap, target.y, endX, target.y);
  ctx.strokeStyle = '#8ab7d8';
  ctx.lineWidth = Math.max(1.2, 2.4 * viewport.value.scale);
  ctx.stroke();
}

function drawRelation(ctx, relation, layoutNodes, width, height) {
  // 暂时隐藏节点关联的虚线
}

function drawNode(ctx, node, width, height) {
  const center = toScreen(node, width, height);
  const boxWidth = node.width * viewport.value.scale;
  const boxHeight = node.height * viewport.value.scale;
  const left = center.x - boxWidth / 2;
  const top = center.y - boxHeight / 2;
  const selected = node.id === props.selectedNodeId;
  const hovering = node.id === hoverNodeId.value;

  ctx.save();
  ctx.shadowColor = node.isRoot ? 'rgba(44, 85, 120, 0.16)' : 'rgba(40, 55, 70, 0.1)';
  ctx.shadowBlur = node.isRoot ? 18 : 10;
  ctx.shadowOffsetY = node.isRoot ? 8 : 5;
  roundedRect(ctx, left, top, boxWidth, boxHeight, 8);
  ctx.fillStyle = node.isRoot ? '#ffffff' : selected ? '#ecf5ff' : '#ffffff';
  ctx.fill();
  ctx.shadowColor = 'transparent';
  ctx.lineWidth = selected || hovering ? 2.4 : 1.4;
  ctx.strokeStyle = node.isRoot ? '#78b6e8' : selected || hovering ? '#409eff' : '#d5e1ec';
  ctx.stroke();

  ctx.fillStyle = node.isRoot ? '#214f77' : '#25384a';
  ctx.font = `${node.isRoot ? 700 : 600} ${Math.max(11, 14 * viewport.value.scale)}px "Microsoft YaHei", "Segoe UI", Arial`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  const lineHeight = 18 * viewport.value.scale;
  const startY = center.y - ((node.lines.length - 1) * lineHeight) / 2;
  node.lines.forEach((line, index) => {
    const suffix = index === 2 && String(node.title).length > line.length ? '...' : '';
    ctx.fillText(`${line}${suffix}`, center.x, startY + index * lineHeight);
  });
  ctx.restore();

  if (!node.isRoot) {
    hitRegions.value.push({ id: node.id, node, left, top, right: left + boxWidth, bottom: top + boxHeight });

    const direction = node.direction ?? 1;
    if (node.childCount > 0 && (node.collapsed || selected)) {
      const toggleX = direction >= 0 ? left + boxWidth + 8 : left - 8;
      drawSubtreeToggle(ctx, toggleX, center.y, node.collapsed ? '+' : '-');
      actionRegions.value.push({ type: 'toggle-subtree', node, x: toggleX, y: center.y, radius: 8 });
    }

    if (props.editable && (hovering || selected)) {
      const actionX = direction >= 0 ? left + boxWidth + 32 : left - 32;
      const addY = center.y - 12;
      const deleteY = center.y + 12;
      drawActionButton(ctx, actionX, addY, '+', '#409eff');
      drawActionButton(ctx, actionX, deleteY, '×', '#d84b4b');
      actionRegions.value.push(
        { type: 'add-child', node, x: actionX, y: addY, radius: 10 },
        { type: 'delete-node', node, x: actionX, y: deleteY, radius: 10 }
      );
    }
  }
}

function drawActionButton(ctx, x, y, label, color) {
  ctx.save();
  ctx.beginPath();
  ctx.arc(x, y, 10, 0, Math.PI * 2);
  ctx.fillStyle = '#ffffff';
  ctx.fill();
  ctx.lineWidth = 1.5;
  ctx.strokeStyle = color;
  ctx.stroke();
  ctx.fillStyle = color;
  ctx.font = '700 13px "Microsoft YaHei", "Segoe UI", Arial';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(label, x, y - 1);
  ctx.restore();
}

function drawSubtreeToggle(ctx, x, y, label) {
  ctx.save();
  ctx.beginPath();
  ctx.arc(x, y, 7, 0, Math.PI * 2);
  ctx.fillStyle = '#ffffff';
  ctx.fill();
  ctx.lineWidth = 1.2;
  ctx.strokeStyle = '#4f7898';
  ctx.stroke();
  ctx.fillStyle = '#4f7898';
  ctx.font = '700 11px "Microsoft YaHei", "Segoe UI", Arial';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(label, x, y - 0.5);
  ctx.restore();
}

function drawEmpty(ctx, width, height) {
  ctx.fillStyle = '#6a7b8c';
  ctx.font = '14px "Microsoft YaHei", "Segoe UI", Arial';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(props.map ? '当前导图暂无节点' : '请选择一个思维导图', width / 2, height / 2);
}

function drawCanvas() {
  const canvas = canvasRef.value;
  const wrapper = wrapRef.value;
  if (!canvas || !wrapper) {
    return;
  }

  const width = Math.max(320, Math.floor(wrapper.clientWidth));
  const height = Math.max(320, Math.floor(wrapper.clientHeight));
  const ratio = window.devicePixelRatio || 1;
  canvas.width = Math.floor(width * ratio);
  canvas.height = Math.floor(height * ratio);

  const ctx = canvas.getContext('2d');
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  hitRegions.value = [];
  actionRegions.value = [];
  drawGrid(ctx, width, height);

  if (!props.map || props.nodes.length === 0) {
    drawEmpty(ctx, width, height);
    return;
  }

  ctx.font = '14px "Microsoft YaHei", "Segoe UI", Arial';
  const layout = createLayout(ctx);
  layout.links.forEach((link) => drawLink(ctx, link.from, link.to, link.direction, width, height));
  props.relations.forEach((relation) => drawRelation(ctx, relation, layout.layoutNodes, width, height));
  layout.layoutNodes.forEach((node) => drawNode(ctx, node, width, height));
}

function getCanvasPoint(event) {
  const rect = canvasRef.value.getBoundingClientRect();
  return { x: event.clientX - rect.left, y: event.clientY - rect.top };
}

function findHit(event) {
  const point = getCanvasPoint(event);
  return hitRegions.value.find((region) => {
    return point.x >= region.left && point.x <= region.right && point.y >= region.top && point.y <= region.bottom;
  }) ?? null;
}

function findAction(event) {
  const point = getCanvasPoint(event);
  return actionRegions.value.find((region) => Math.hypot(point.x - region.x, point.y - region.y) <= region.radius) ?? null;
}

function handlePointerDown(event) {
  const canvas = canvasRef.value;
  const wrapper = wrapRef.value;
  if (!canvas || !wrapper) {
    return;
  }

  canvas.setPointerCapture(event.pointerId);
  const point = getCanvasPoint(event);
  const action = findAction(event);
  const hit = findHit(event);
  interaction = {
    type: action ? 'action' : hit ? 'node' : 'canvas',
    action,
    node: action?.node ?? hit?.node ?? null,
    startX: point.x,
    startY: point.y,
    moved: false,
    startViewport: { ...viewport.value }
  };
}

function handlePointerMove(event) {
  const wrapper = wrapRef.value;
  if (!wrapper) {
    return;
  }

  const point = getCanvasPoint(event);
  if (interaction) {
    const deltaX = point.x - interaction.startX;
    const deltaY = point.y - interaction.startY;
    interaction.moved = interaction.moved || Math.hypot(deltaX, deltaY) > 4;
    if (interaction.type === 'canvas') {
      viewport.value = {
        ...viewport.value,
        x: interaction.startViewport.x + deltaX,
        y: interaction.startViewport.y + deltaY
      };
      scheduleDraw();
      return;
    }
  }

  const action = findAction(event);
  const hit = findHit(event);
  if (action) {
    const nextHoverId = action.node.id;
    if (nextHoverId !== hoverNodeId.value) {
      hoverNodeId.value = nextHoverId;
      scheduleDraw();
    }
    return;
  }
  const nextHoverId = hit?.id ?? null;
  if (nextHoverId !== hoverNodeId.value) {
    hoverNodeId.value = nextHoverId;
    scheduleDraw();
  }
}

function handlePointerUp(event) {
  canvasRef.value?.releasePointerCapture(event.pointerId);
  const current = interaction;
  interaction = null;
  if (!current || !current.node) {
    return;
  }

  if (current.moved) {
    return;
  }

  if (current.type === 'action') {
    emit('select-node', current.node.id);
    if (current.action?.type === 'toggle-subtree') {
      toggleSubtree(current.node.id);
      return;
    }
    if (current.action?.type === 'add-child') {
      emit('create-node', { parentId: current.node.id, title: '新子节点', content: '', orderNo: props.nodes.length + 1 });
    }
    if (current.action?.type === 'delete-node') {
      emit('delete-node');
    }
    return;
  }

  emit('select-node', current.node.id);
  if (props.previewOnClick) {
    emit('preview-node', current.node);
  }
}

function handlePointerLeave() {
  hoverNodeId.value = null;
  scheduleDraw();
}

function zoomAt(factor) {
  viewport.value = {
    ...viewport.value,
    scale: Math.max(0.35, Math.min(2.8, viewport.value.scale * factor))
  };
  scheduleDraw();
}

function handleWheel(event) {
  event.preventDefault();
  zoomAt(event.deltaY > 0 ? 0.9 : 1.1);
}

function resetView() {
  collapsedNodeIds.value = new Set();
  fitView();
  emit('refresh-map');
}

function toggleSubtree(nodeId) {
  const next = new Set(collapsedNodeIds.value);
  if (next.has(nodeId)) {
    next.delete(nodeId);
  } else {
    next.add(nodeId);
  }
  collapsedNodeIds.value = next;
  scheduleDraw();
}

function pruneCollapsedNodes() {
  if (collapsedNodeIds.value.size === 0) {
    return;
  }

  const nodeIds = new Set(props.nodes.map((node) => node.id));
  const next = new Set([...collapsedNodeIds.value].filter((id) => nodeIds.has(id)));
  if (next.size !== collapsedNodeIds.value.size) {
    collapsedNodeIds.value = next;
  }
}

function createRootNode() {
  emit('create-node', { parentId: null, title: '新根节点', content: '', orderNo: props.nodes.length + 1 });
}

function createChildNode() {
  emit('create-node', {
    parentId: selectedNode.value?.id ?? null,
    title: selectedNode.value ? '新子节点' : '新根节点',
    content: '',
    orderNo: props.nodes.length + 1
  });
}

function saveSelectedNode() {
  emit('update-node', {
    title: editorForm.value.title,
    content: editorForm.value.content,
    orderNo: Number(editorForm.value.orderNo) || 0
  });
}

watch(() => [props.map?.id, props.nodes.length], async () => {
  await nextTick();
  fitView();
});

watch(() => [props.nodes, props.relations, props.selectedNodeId], () => {
  pruneCollapsedNodes();
  scheduleDraw();
}, { deep: true });

watch(() => props.map?.id, () => {
  collapsedNodeIds.value = new Set();
});

watch(selectedNode, (node) => {
  editorForm.value = node
    ? { title: node.title, content: node.content ?? '', orderNo: node.orderNo ?? 0 }
    : { title: '', content: '', orderNo: 1 };
}, { immediate: true });

onMounted(async () => {
  await nextTick();
  resizeObserver = new ResizeObserver(() => scheduleDraw());
  if (wrapRef.value) {
    resizeObserver.observe(wrapRef.value);
  }
  fitView();
});

onBeforeUnmount(() => {
  window.cancelAnimationFrame(rafId);
  resizeObserver?.disconnect();
});
</script>

<template>
  <section class="canvas-panel">
    <div class="section-heading">
      <h2>{{ map?.title ?? '未选择导图' }}</h2>
      <span>{{ nodes.length }} 个节点</span>
    </div>
    <div class="canvas-toolbar">
      <div class="canvas-tool-group">
        <el-button :icon="ZoomOut" @click="zoomAt(0.9)">缩小</el-button>
        <el-button :icon="ZoomIn" @click="zoomAt(1.1)">放大</el-button>
        <el-button :icon="Refresh" :disabled="loading || !map" data-testid="reset-canvas" @click="resetView">重置</el-button>
      </div>
      <div v-if="editable" class="canvas-tool-group canvas-primary-tools">
        <el-button type="primary" :icon="Plus" :disabled="loading || !map" @click="createRootNode">根节点</el-button>
        <el-button :icon="Plus" :disabled="loading || !map" @click="createChildNode">子节点</el-button>
        <el-button type="danger" :icon="Delete" :disabled="loading || !selectedNode" @click="$emit('delete-node')">删除</el-button>
      </div>
    </div>
    <div ref="wrapRef" class="mind-map-canvas-wrap" data-testid="mind-map-canvas">
      <canvas
        ref="canvasRef"
        aria-label="思维导图画布"
        :class="{ locked: !editable }"
        @pointerdown="handlePointerDown"
        @pointermove="handlePointerMove"
        @pointerup="handlePointerUp"
        @pointercancel="handlePointerUp"
        @pointerleave="handlePointerLeave"
        @wheel="handleWheel"
      />
      <div v-if="editable && !hideCanvasEditor" class="canvas-editor" data-testid="canvas-node-editor">
        <div class="canvas-editor-title">
          <el-icon><EditPen /></el-icon>
          <div>
            <h2>节点属性</h2>
            <p>{{ selectedNode ? '修改后点击保存节点。' : '点击画布中的节点后编辑内容。' }}</p>
          </div>
          <span>{{ selectedNode ? `#${selectedNode.id}` : '未选择' }}</span>
        </div>
        <el-input v-model="editorForm.title" data-testid="canvas-node-title" placeholder="节点标题" />
        <div class="content-header" style="display: flex; justify-content: space-between; align-items: center; margin-top: 4px;">
          <span style="font-size: 13px; color: #606266; font-weight: 500;">节点内容</span>
          <el-button
            v-if="selectedNode"
            link
            type="primary"
            :icon="Link"
            @click="showRefDialog = true"
          >
            插入引用
          </el-button>
        </div>
        <el-input v-model="editorForm.content" data-testid="canvas-node-content" type="textarea" :rows="3" placeholder="节点内容。输入 [[ 快捷引用。" @keyup="onContentKeyup" />
        
        <div style="display: flex; align-items: center; gap: 8px;">
          <span style="white-space: nowrap; color: #606266;">同级排序</span>
          <el-input-number v-model="editorForm.orderNo" data-testid="canvas-node-order" :min="0" style="width: 100%;" />
        </div>
        <div class="canvas-editor-actions">
          <el-button type="primary" :disabled="loading || !selectedNode" @click="saveSelectedNode">保存节点</el-button>
        </div>
      </div>
    </div>

    <el-dialog v-model="showRefDialog" title="插入节点引用 (全局)" width="min(400px, calc(100vw - 32px))" append-to-body @opened="onRefDialogOpened">
      <el-select
        v-model="searchKeyword"
        filterable
        remote
        reserve-keyword
        placeholder="输入关键词搜索全库节点"
        :remote-method="handleSearch"
        :loading="searching"
        style="width: 100%"
        popper-class="ref-dialog-select"
        class="ref-dialog-select-wrap"
        @change="(id) => {
          const node = searchResults.find(n => n.id === id);
          if (node) insertReference(node);
        }"
      >
        <el-option
          v-for="item in searchResults"
          :key="item.id"
          :label="item.title"
          :value="item.id"
        >
          <el-tooltip
            effect="dark"
            placement="right"
            :show-after="300"
          >
            <template #content>
              <div class="search-preview-tooltip">
                <div class="tooltip-map-tag" v-if="item.mapTitle">
                  所属导图：{{ item.mapTitle }}
                </div>
                <div v-if="item.content" class="markdown-body mini" v-html="renderMarkdown(item.content)"></div>
                <div v-else class="muted">暂无详细内容</div>
              </div>
            </template>
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
              <span style="overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 240px;">{{ item.title }}</span>
              <span style="color: var(--el-text-color-secondary); font-size: 12px; margin-left: 8px;">#{{ item.id }}</span>
            </div>
          </el-tooltip>
        </el-option>
      </el-select>
      <template #footer>
        <el-button @click="showRefDialog = false">取消</el-button>
      </template>
    </el-dialog>
  </section>
</template>
