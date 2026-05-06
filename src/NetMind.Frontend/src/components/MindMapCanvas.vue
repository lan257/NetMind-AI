<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { Delete, EditPen, Plus, Refresh, ZoomIn, ZoomOut } from '@element-plus/icons-vue';

const props = defineProps({
  map: { type: Object, default: null },
  nodes: { type: Array, default: () => [] },
  relations: { type: Array, default: () => [] },
  selectedNodeId: { type: [Number, String, null], default: null },
  previewOnClick: { type: Boolean, default: true },
  editable: { type: Boolean, default: false },
  loading: { type: Boolean, default: false }
});

const emit = defineEmits(['select-node', 'preview-node', 'create-node', 'update-node', 'delete-node']);

const canvasRef = ref(null);
const wrapRef = ref(null);
const hoverNodeId = ref(null);
const hitRegions = ref([]);
const actionRegions = ref([]);
const manualPositions = ref(new Map());
const viewport = ref({ x: 0, y: 0, scale: 1 });
const editorForm = ref({ title: '', content: '', orderNo: 1 });
let resizeObserver = null;
let rafId = 0;
let interaction = null;

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
  if (children.length === 0) {
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
    const manual = manualPositions.value.get(node.id);
    const graphNode = {
      ...node,
      x: manual?.x ?? direction * (rootGap + (depth - 1) * levelGap),
      y: manual?.y ?? y,
      width: widthValue,
      height: heightValue,
      lines,
      direction
    };
    layoutNodes.push(graphNode);
    return graphNode;
  };

  sides.forEach((side) => {
    const totalLeaves = side.nodes.reduce((total, node) => total + countSubtreeLeaves(node, childrenByParent), 0);
    let cursor = -((Math.max(totalLeaves, 1) - 1) * leafGap) / 2;

    const walk = (node, parent, depth) => {
      const children = childrenByParent.get(node.id) ?? [];
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

function toWorld(point, width, height) {
  return {
    x: (point.x - width / 2 - viewport.value.x) / viewport.value.scale,
    y: (point.y - height / 2 - viewport.value.y) / viewport.value.scale
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
  const source = layoutNodes.find((node) => node.id === relation.sourceId);
  const target = layoutNodes.find((node) => node.id === relation.targetId);
  if (!source || !target) {
    return;
  }
  const start = toScreen(source, width, height);
  const end = toScreen(target, width, height);
  ctx.save();
  ctx.setLineDash([6, 6]);
  ctx.beginPath();
  ctx.moveTo(start.x, start.y);
  ctx.lineTo(end.x, end.y);
  ctx.strokeStyle = 'rgba(47, 111, 115, 0.42)';
  ctx.lineWidth = Math.max(1, 1.6 * viewport.value.scale);
  ctx.stroke();
  ctx.setLineDash([]);
  ctx.fillStyle = '#2f6f73';
  ctx.font = '12px "Microsoft YaHei", "Segoe UI", Arial';
  ctx.textAlign = 'center';
  ctx.fillText(relation.relationType ?? '关联', (start.x + end.x) / 2, (start.y + end.y) / 2 - 6);
  ctx.restore();
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

    if (props.editable && (hovering || selected)) {
      const direction = node.direction ?? 1;
      const actionX = direction >= 0 ? left + boxWidth + 14 : left - 14;
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
    type: action ? 'action' : hit && props.editable ? 'node' : 'pan',
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
  const width = Math.max(320, Math.floor(wrapper.clientWidth));
  const height = Math.max(320, Math.floor(wrapper.clientHeight));

  if (interaction) {
    const deltaX = point.x - interaction.startX;
    const deltaY = point.y - interaction.startY;
    interaction.moved = interaction.moved || Math.hypot(deltaX, deltaY) > 4;

    if (interaction.type === 'pan') {
      viewport.value = {
        ...viewport.value,
        x: interaction.startViewport.x + deltaX,
        y: interaction.startViewport.y + deltaY
      };
      scheduleDraw();
      return;
    }

    if (props.editable && interaction.type === 'node' && interaction.node) {
      const world = toWorld(point, width, height);
      const next = new Map(manualPositions.value);
      next.set(interaction.node.id, world);
      manualPositions.value = next;
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
  if (!current || current.moved || !current.node) {
    return;
  }

  if (current.type === 'action') {
    emit('select-node', current.node.id);
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
  manualPositions.value = new Map();
  fitView();
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
  manualPositions.value = new Map();
  await nextTick();
  fitView();
});

watch(() => [props.nodes, props.relations, props.selectedNodeId], () => scheduleDraw(), { deep: true });

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
        <el-button :icon="Refresh" @click="resetView">重置</el-button>
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
      <div v-if="editable" class="canvas-editor" data-testid="canvas-node-editor">
        <div class="canvas-editor-title">
          <el-icon><EditPen /></el-icon>
          <div>
            <h2>节点属性</h2>
            <p>{{ selectedNode ? '修改后点击保存，拖动节点可临时整理当前画布。' : '点击画布中的节点后编辑内容。' }}</p>
          </div>
          <span>{{ selectedNode ? `#${selectedNode.id}` : '未选择' }}</span>
        </div>
        <el-input v-model="editorForm.title" data-testid="canvas-node-title" placeholder="节点标题" />
        <el-input v-model="editorForm.content" data-testid="canvas-node-content" type="textarea" :rows="3" placeholder="节点内容" />
        
        <div style="display: flex; align-items: center; gap: 8px;">
          <span style="white-space: nowrap; color: #606266;">同级排序</span>
          <el-input-number v-model="editorForm.orderNo" data-testid="canvas-node-order" :min="0" style="width: 220px;" />
        </div>
        <div class="canvas-editor-actions">
          <el-button type="primary" :disabled="loading || !selectedNode" @click="saveSelectedNode">保存节点</el-button>
        </div>
      </div>
    </div>
  </section>
</template>
