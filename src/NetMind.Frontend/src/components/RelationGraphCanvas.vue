<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';

const props = defineProps({
  centerNode: { type: Object, default: null },
  nodes: { type: Array, default: () => [] },
  relations: { type: Array, default: () => [] },
  height: { type: Number, default: 240 },
  interactive: { type: Boolean, default: true },
  nodeDraggable: { type: Boolean, default: true },
  showLabels: { type: Boolean, default: true }
});

const emit = defineEmits(['preview-node']);

const canvasRef = ref(null);
const wrapRef = ref(null);
const hitRegions = ref([]);
const hoverNodeKey = ref(null);
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

function createBranchNode(node, key, depth, parentKey = null) {
  return {
    key,
    id: node.id,
    node,
    depth,
    parentKey
  };
}

function getRelatedBranches(nodeId, excludedNodeId = null) {
  return props.relations
    .map((relation, index) => {
      const relatedId = relation.sourceId === nodeId
        ? relation.targetId
        : relation.targetId === nodeId
          ? relation.sourceId
          : null;

      if (relatedId == null || relatedId === excludedNodeId) {
        return null;
      }

      const relatedNode = nodeById.value.get(relatedId);
      return relatedNode ? { relation, relatedNode, index } : null;
    })
    .filter(Boolean);
}

function createOccurrenceKey(parentKey, branch, siblingIndex) {
  const relationKey = branch.relation.id ?? `${branch.relation.sourceId}-${branch.relation.targetId}-${branch.index}`;
  return `${parentKey}-${relationKey}-${branch.relatedNode.id}-${siblingIndex}`;
}

const relationTree = computed(() => {
  if (!props.centerNode) {
    return { nodes: [], links: [] };
  }

  const center = createBranchNode(props.centerNode, 'center', 0);
  const nodes = [center];
  const links = [];

  getRelatedBranches(props.centerNode.id).forEach((branch, index) => {
    const firstKey = createOccurrenceKey(center.key, branch, index);
    const firstNode = createBranchNode(branch.relatedNode, firstKey, 1, center.key);
    nodes.push(firstNode);
    links.push({
      key: `${center.key}-${firstKey}`,
      sourceKey: center.key,
      targetKey: firstKey,
      relation: branch.relation,
      depth: 1
    });

    getRelatedBranches(firstNode.id, props.centerNode.id).forEach((childBranch, childIndex) => {
      const childKey = createOccurrenceKey(firstKey, childBranch, childIndex);
      nodes.push(createBranchNode(childBranch.relatedNode, childKey, 2, firstKey));
      links.push({
        key: `${firstKey}-${childKey}`,
        sourceKey: firstKey,
        targetKey: childKey,
        relation: childBranch.relation,
        depth: 2
      });
    });
  });

  return { nodes, links };
});

function scheduleDraw() {
  window.cancelAnimationFrame(rafId);
  rafId = window.requestAnimationFrame(() => drawCanvas());
}

function getNodeRadius(node) {
  const labeledRadii = [22, 17, 13];
  const compactRadii = [15, 10, 8];
  const radii = props.showLabels ? labeledRadii : compactRadii;
  return radii[node.depth] ?? radii[2];
}

function createLayoutPositions(width, height) {
  const positions = new Map();
  const centerNode = relationTree.value.nodes.find((node) => node.depth === 0);
  if (!centerNode) {
    return positions;
  }

  const centerPosition = manualPositions.value.get(centerNode.key) ?? { x: 0, y: 0 };
  positions.set(centerNode.key, centerPosition);

  const outerPadding = props.showLabels ? 68 : 28;
  const outerRadius = Math.max(44, Math.min(width, height) / 2 - outerPadding);
  const firstRingRadius = outerRadius * 0.58;
  const childRingRadius = outerRadius * 0.34;
  const firstRingNodes = relationTree.value.nodes.filter((node) => node.depth === 1);

  firstRingNodes.forEach((node, index) => {
    const angle = ((Math.PI * 2) / Math.max(firstRingNodes.length, 1)) * index - Math.PI / 2;
    const base = {
      x: centerPosition.x + Math.cos(angle) * firstRingRadius,
      y: centerPosition.y + Math.sin(angle) * firstRingRadius
    };
    const position = manualPositions.value.get(node.key) ?? base;
    positions.set(node.key, position);

    const children = relationTree.value.nodes.filter((child) => child.parentKey === node.key);
    children.forEach((child, childIndex) => {
      const spread = children.length === 1 ? 0 : Math.min(Math.PI * 0.92, Math.PI / 4 + children.length * 0.2);
      const childAngle = children.length === 1
        ? angle
        : angle - spread / 2 + (spread * childIndex) / Math.max(children.length - 1, 1);
      const childBase = {
        x: position.x + Math.cos(childAngle) * childRingRadius,
        y: position.y + Math.sin(childAngle) * childRingRadius
      };
      positions.set(child.key, manualPositions.value.get(child.key) ?? childBase);
    });
  });

  return positions;
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

  if (!props.centerNode || relationTree.value.nodes.length === 0) {
    ctx.fillStyle = '#6a7b8c';
    ctx.font = '14px "Microsoft YaHei", "Segoe UI", Arial';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('暂无关联节点', width / 2, height / 2);
    return;
  }

  const positions = createLayoutPositions(width, height);
  const nodeByKey = new Map(relationTree.value.nodes.map((node) => [node.key, node]));

  relationTree.value.links.forEach((link) => {
    const source = positions.get(link.sourceKey);
    const target = positions.get(link.targetKey);
    const sourceNode = nodeByKey.get(link.sourceKey);
    const targetNode = nodeByKey.get(link.targetKey);
    if (!source || !target || !sourceNode || !targetNode) {
      return;
    }
    const start = toScreen(source, width, height);
    const end = toScreen(target, width, height);
    
    // 缩短连线到圆圈边缘。
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    const angle = Math.atan2(dy, dx);
    const sourceRadius = getNodeRadius(sourceNode) * viewport.value.scale;
    const targetRadius = getNodeRadius(targetNode) * viewport.value.scale;
    
    const lineStartX = start.x + Math.cos(angle) * sourceRadius;
    const lineStartY = start.y + Math.sin(angle) * sourceRadius;
    const lineEndX = end.x - Math.cos(angle) * targetRadius;
    const lineEndY = end.y - Math.sin(angle) * targetRadius;

    ctx.beginPath();
    ctx.moveTo(lineStartX, lineStartY);
    ctx.lineTo(lineEndX, lineEndY);
    ctx.strokeStyle = '#8ab7d8';
    ctx.lineWidth = Math.max(0.9, (link.depth === 1 ? 1.8 : 1.35) * viewport.value.scale);
    ctx.stroke();

    if (props.showLabels) {
      const midX = (start.x + end.x) / 2;
      const midY = (start.y + end.y) / 2;
      ctx.fillStyle = '#60798e';
      ctx.font = `${link.depth === 1 ? 12 : 11}px "Microsoft YaHei", "Segoe UI", Arial`;
      ctx.textAlign = 'center';
      ctx.fillText(link.relation.relationType ?? '关联', midX, midY - 6);
    }
  });

  relationTree.value.nodes.forEach((node) => {
    const point = positions.get(node.key);
    if (!point) {
      return;
    }
    const screen = toScreen(point, width, height);
    const isCenter = node.depth === 0;
    const radius = getNodeRadius(node) * viewport.value.scale;
    const hovering = node.key === hoverNodeKey.value;

    ctx.beginPath();
    ctx.arc(screen.x, screen.y, radius, 0, Math.PI * 2);
    ctx.fillStyle = isCenter ? '#ecf5ff' : node.node.isExternal ? '#f9f9f9' : '#ffffff';
    ctx.fill();
    ctx.lineWidth = hovering || isCenter ? 2.4 : 1.4;
    ctx.setLineDash(node.node.isExternal ? [4, 4] : []);
    ctx.strokeStyle = isCenter ? '#409eff' : hovering ? '#409eff' : '#cbd7e2';
    ctx.stroke();
    ctx.setLineDash([]);

    if (props.showLabels) {
      ctx.fillStyle = '#25384a';
      ctx.font = `${isCenter ? 700 : 600} ${Math.max(11, (isCenter ? 13 : 12) * viewport.value.scale)}px "Microsoft YaHei", "Segoe UI", Arial`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      const title = String(node.node.title ?? '未命名节点');
      ctx.fillText(title.length > 10 ? `${title.slice(0, 10)}...` : title, screen.x, screen.y + radius + 12);
    }
    hitRegions.value.push({
      graphNode: node,
      node: node.node,
      key: node.key,
      x: screen.x,
      y: screen.y,
      radius: Math.max(radius, props.showLabels ? radius : 12)
    });
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
    graphNode: hit?.graphNode ?? null,
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

  if (props.interactive && props.nodeDraggable && interaction?.type === 'node' && interaction.graphNode) {
    const world = toWorld(point, width, height);
    const next = new Map(manualPositions.value);
    next.set(interaction.graphNode.key, world);
    manualPositions.value = next;
    scheduleDraw();
    return;
  }

  const hit = findHit(event);
  const nextHoverKey = hit?.key ?? null;
  if (nextHoverKey !== hoverNodeKey.value) {
    hoverNodeKey.value = nextHoverKey;
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
