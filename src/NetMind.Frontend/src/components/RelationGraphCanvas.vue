<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';

const props = defineProps({
  centerNode: { type: Object, default: null },
  nodes: { type: Array, default: () => [] },
  relations: { type: Array, default: () => [] },
  height: { type: Number, default: 240 },
  interactive: { type: Boolean, default: true },
  nodeDraggable: { type: Boolean, default: true }
});

const emit = defineEmits(['preview-node']);

const canvasRef = ref(null);
const wrapRef = ref(null);
const hitRegions = ref([]);
const hoverNodeId = ref(null);
const manualPositions = ref(new Map());
const viewport = ref({ x: 0, y: 0, scale: 1 });
let resizeObserver = null;
let rafId = 0;
let interaction = null;
let suppressClick = false;

const nodeById = computed(() => {
  const result = new Map();
  // 先加入当前导图的所有节点
  props.nodes.forEach((node) => result.set(node.id, node));
  
  // 再加入关联中引用的跨图节点信息
  props.relations.forEach((rel) => {
    if (!result.has(rel.sourceId) && rel.sourceTitle) {
      result.set(rel.sourceId, { 
        id: rel.sourceId, 
        title: rel.sourceTitle, 
        mapId: rel.sourceMapId,
        isExternal: true 
      });
    }
    if (!result.has(rel.targetId) && rel.targetTitle) {
      result.set(rel.targetId, { 
        id: rel.targetId, 
        title: rel.targetTitle, 
        mapId: rel.targetMapId,
        isExternal: true 
      });
    }
  });
  return result;
});

const relationGraph = computed(() => {
  if (!props.centerNode) {
    return { nodes: [], links: [], depthById: new Map() };
  }

  const included = new Set([props.centerNode.id]);
  const depthById = new Map([[props.centerNode.id, 0]]);
  let frontier = new Set([props.centerNode.id]);

  for (let depth = 0; depth < 2; depth += 1) {
    const next = new Set();
    props.relations.forEach((relation) => {
      if (frontier.has(relation.sourceId) && nodeById.value.has(relation.targetId)) {
        next.add(relation.targetId);
      }
      if (frontier.has(relation.targetId) && nodeById.value.has(relation.sourceId)) {
        next.add(relation.sourceId);
      }
    });
    const nextFrontier = new Set([...next].filter((id) => !included.has(id)));
    nextFrontier.forEach((id) => {
      included.add(id);
      depthById.set(id, depth + 1);
    });
    frontier = nextFrontier;
  }

  const graphNodes = [...included]
    .map((id) => nodeById.value.get(id))
    .filter(Boolean);
  const links = props.relations.filter((relation) => included.has(relation.sourceId) && included.has(relation.targetId));
  return { nodes: graphNodes, links, depthById };
});

function scheduleDraw() {
  window.cancelAnimationFrame(rafId);
  rafId = window.requestAnimationFrame(() => drawCanvas());
}

function basePosition(node, index, total, width, height) {
  if (node.id === props.centerNode?.id) {
    return { x: 0, y: 0 };
  }

  const depth = relationGraph.value.depthById.get(node.id) ?? 1;
  const available = Math.min(width, height);
  const radius = Math.min(
    available * (depth === 1 ? 0.42 : 0.62),
    depth === 1 ? 180 : 280
  );
  const ringNodes = relationGraph.value.nodes.filter((item) => (relationGraph.value.depthById.get(item.id) ?? 1) === depth);
  const ringIndex = ringNodes.findIndex((item) => item.id === node.id);
  const angleOffset = depth === 1 ? -Math.PI / 2 : -Math.PI / 2 + Math.PI / Math.max(ringNodes.length, 2);
  const angle = ((Math.PI * 2) / Math.max(ringNodes.length, 1)) * Math.max(ringIndex, 0) + angleOffset;
  return { x: Math.cos(angle) * radius, y: Math.sin(angle) * radius };
}

function getNodePosition(node, index, total, width, height) {
  return manualPositions.value.get(node.id) ?? basePosition(node, index, total, width, height);
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
  ctx.strokeStyle = 'rgba(60, 130, 160, 0.08)';
  ctx.lineWidth = 1;
  const grid = 26 * viewport.value.scale;
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

function drawCanvas() {
  const canvas = canvasRef.value;
  const wrapper = wrapRef.value;
  if (!canvas || !wrapper) {
    return;
  }

  const width = Math.max(280, Math.floor(wrapper.clientWidth));
  const height = Math.max(220, Math.floor(wrapper.clientHeight));
  const ratio = window.devicePixelRatio || 1;
  canvas.width = Math.floor(width * ratio);
  canvas.height = Math.floor(height * ratio);

  const ctx = canvas.getContext('2d');
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  hitRegions.value = [];
  drawGrid(ctx, width, height);

  if (!props.centerNode || relationGraph.value.nodes.length === 0) {
    ctx.fillStyle = '#6a7b8c';
    ctx.font = '14px "Microsoft YaHei", "Segoe UI", Arial';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('暂无关联节点', width / 2, height / 2);
    return;
  }

  const positions = new Map();
  relationGraph.value.nodes.forEach((node, index) => {
    positions.set(node.id, getNodePosition(node, index, relationGraph.value.nodes.length, width, height));
  });

  relationGraph.value.links.forEach((relation) => {
    const source = positions.get(relation.sourceId);
    const target = positions.get(relation.targetId);
    if (!source || !target) {
      return;
    }
    const start = toScreen(source, width, height);
    const end = toScreen(target, width, height);
    
    // 计算箭头位置
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    const angle = Math.atan2(dy, dx);
    const sourceRadius = (relation.sourceId === props.centerNode?.id ? 28 : (relationGraph.value.depthById.get(relation.sourceId) === 1 ? 22 : 18)) * viewport.value.scale;
    const targetRadius = (relation.targetId === props.centerNode?.id ? 28 : (relationGraph.value.depthById.get(relation.targetId) === 1 ? 22 : 18)) * viewport.value.scale;
    
    // 缩短线条，使其始于并止于圆圈边缘
    const lineStartX = start.x + Math.cos(angle) * sourceRadius;
    const lineStartY = start.y + Math.sin(angle) * sourceRadius;
    const lineEndX = end.x - Math.cos(angle) * targetRadius;
    const lineEndY = end.y - Math.sin(angle) * targetRadius;

    ctx.beginPath();
    ctx.moveTo(lineStartX, lineStartY);
    ctx.lineTo(lineEndX, lineEndY);
    ctx.strokeStyle = '#8ab7d8';
    ctx.lineWidth = Math.max(1, 2 * viewport.value.scale);
    ctx.stroke();

    // 绘制箭头
    const arrowSize = 8 * viewport.value.scale;
    ctx.beginPath();
    ctx.moveTo(lineEndX, lineEndY);
    ctx.lineTo(lineEndX - arrowSize * Math.cos(angle - Math.PI / 6), lineEndY - arrowSize * Math.sin(angle - Math.PI / 6));
    ctx.lineTo(lineEndX - arrowSize * Math.cos(angle + Math.PI / 6), lineEndY - arrowSize * Math.sin(angle + Math.PI / 6));
    ctx.closePath();
    ctx.fillStyle = '#8ab7d8';
    ctx.fill();

    const midX = (start.x + end.x) / 2;
    const midY = (start.y + end.y) / 2;
    ctx.fillStyle = '#60798e';
    ctx.font = '12px "Microsoft YaHei", "Segoe UI", Arial';
    ctx.textAlign = 'center';
    ctx.fillText(relation.relationType ?? '关联', midX, midY - 6);
  });

  relationGraph.value.nodes.forEach((node, index) => {
    const point = positions.get(node.id);
    const screen = toScreen(point, width, height);
    const isCenter = node.id === props.centerNode.id;
    const nodeDepth = relationGraph.value.depthById.get(node.id) ?? 1;
    const radius = (isCenter ? 28 : nodeDepth === 1 ? 22 : 18) * viewport.value.scale;
    const hovering = node.id === hoverNodeId.value;

    ctx.beginPath();
    ctx.arc(screen.x, screen.y, radius, 0, Math.PI * 2);
    ctx.fillStyle = isCenter ? '#ecf5ff' : node.isExternal ? '#f9f9f9' : '#ffffff';
    ctx.fill();
    ctx.lineWidth = hovering || isCenter ? 2.4 : 1.4;
    ctx.setLineDash(node.isExternal ? [4, 4] : []);
    ctx.strokeStyle = isCenter ? '#409eff' : hovering ? '#409eff' : '#cbd7e2';
    ctx.stroke();
    ctx.setLineDash([]);

    ctx.fillStyle = '#25384a';
    ctx.font = `${isCenter ? 700 : 600} ${Math.max(11, 13 * viewport.value.scale)}px "Microsoft YaHei", "Segoe UI", Arial`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    const title = String(node.title ?? '未命名节点');
    ctx.fillText(title.length > 8 ? `${title.slice(0, 8)}...` : title, screen.x, screen.y);
    hitRegions.value.push({ node, x: screen.x, y: screen.y, radius });
  });
}

function getCanvasPoint(event) {
  const rect = canvasRef.value.getBoundingClientRect();
  return { x: event.clientX - rect.left, y: event.clientY - rect.top };
}

function findHit(event) {
  const point = getCanvasPoint(event);
  return hitRegions.value.find((region) => {
    return Math.hypot(point.x - region.x, point.y - region.y) <= region.radius;
  }) ?? null;
}

function handlePointerDown(event) {
  if (!props.interactive) {
    return;
  }

  const canvas = canvasRef.value;
  const wrapper = wrapRef.value;
  if (!canvas || !wrapper) {
    return;
  }

  canvas.setPointerCapture(event.pointerId);
  const point = getCanvasPoint(event);
  const hit = findHit(event);
  const width = Math.max(280, Math.floor(wrapper.clientWidth));
  const height = Math.max(220, Math.floor(wrapper.clientHeight));

  interaction = {
    type: hit && props.nodeDraggable ? 'node' : 'pan',
    node: hit?.node ?? null,
    startX: point.x,
    startY: point.y,
    moved: false,
    startViewport: { ...viewport.value },
    startWorld: hit ? toWorld(point, width, height) : null
  };
}

function handlePointerMove(event) {
  const wrapper = wrapRef.value;
  if (!wrapper) {
    return;
  }

  const point = getCanvasPoint(event);
  const width = Math.max(280, Math.floor(wrapper.clientWidth));
  const height = Math.max(220, Math.floor(wrapper.clientHeight));

  if (interaction) {
    interaction.moved = interaction.moved || Math.hypot(point.x - interaction.startX, point.y - interaction.startY) > 4;
  }

  if (props.interactive && interaction?.type === 'pan') {
    viewport.value = {
      ...viewport.value,
      x: interaction.startViewport.x + point.x - interaction.startX,
      y: interaction.startViewport.y + point.y - interaction.startY
    };
    scheduleDraw();
    return;
  }

  if (props.interactive && props.nodeDraggable && interaction?.type === 'node' && interaction.node) {
    const world = toWorld(point, width, height);
    const next = new Map(manualPositions.value);
    next.set(interaction.node.id, world);
    manualPositions.value = next;
    scheduleDraw();
    return;
  }

  const hit = findHit(event);
  const nextHoverId = hit?.node.id ?? null;
  if (nextHoverId !== hoverNodeId.value) {
    hoverNodeId.value = nextHoverId;
    scheduleDraw();
  }
}

function handlePointerUp(event) {
  canvasRef.value?.releasePointerCapture(event.pointerId);
  suppressClick = Boolean(interaction?.moved);
  interaction = null;
}

function handleWheel(event) {
  if (!props.interactive) {
    return;
  }

  event.preventDefault();
  const factor = event.deltaY > 0 ? 0.9 : 1.1;
  viewport.value = {
    ...viewport.value,
    scale: Math.max(0.45, Math.min(2.4, viewport.value.scale * factor))
  };
  scheduleDraw();
}

function handleClick(event) {
  if (suppressClick) {
    suppressClick = false;
    return;
  }
  const hit = findHit(event);
  if (hit) {
    emit('preview-node', hit.node);
  }
}

watch(() => [props.centerNode, props.nodes, props.relations], () => {
  manualPositions.value = new Map();
  viewport.value = { x: 0, y: 0, scale: 1 };
  scheduleDraw();
}, { deep: true });

onMounted(async () => {
  await nextTick();
  resizeObserver = new ResizeObserver(() => scheduleDraw());
  if (wrapRef.value) {
    resizeObserver.observe(wrapRef.value);
  }
  scheduleDraw();
});

onBeforeUnmount(() => {
  window.cancelAnimationFrame(rafId);
  resizeObserver?.disconnect();
});
</script>

<template>
  <div ref="wrapRef" class="relation-graph-canvas" :style="{ height: `${height}px` }">
    <canvas
      ref="canvasRef"
      aria-label="节点关联画布"
      @click="handleClick"
      :class="{ locked: !interactive }"
      @pointerdown="handlePointerDown"
      @pointermove="handlePointerMove"
      @pointerup="handlePointerUp"
      @pointercancel="handlePointerUp"
      @wheel="handleWheel"
    />
  </div>
</template>
