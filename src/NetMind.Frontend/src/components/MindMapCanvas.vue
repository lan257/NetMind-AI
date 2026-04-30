<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';

const props = defineProps({
  map: { type: Object, default: null },
  nodes: { type: Array, default: () => [] },
  selectedNodeId: { type: [Number, String, null], default: null },
  previewOnClick: { type: Boolean, default: true }
});

const emit = defineEmits(['select-node', 'preview-node']);

const canvasRef = ref(null);
const wrapRef = ref(null);
const hoverNodeId = ref(null);
const hitRegions = ref([]);
let resizeObserver = null;
let rafId = 0;

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

function createLayout(ctx, width, height) {
  const childrenByParent = getChildrenByParent();
  const rootChildren = childrenByParent.get(0) ?? [];
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
  rootChildren.forEach((node, index) => {
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

  const bounds = layoutNodes.reduce((box, node) => ({
    minX: Math.min(box.minX, node.x - node.width / 2),
    maxX: Math.max(box.maxX, node.x + node.width / 2),
    minY: Math.min(box.minY, node.y - node.height / 2),
    maxY: Math.max(box.maxY, node.y + node.height / 2)
  }), { minX: 0, maxX: 0, minY: 0, maxY: 0 });

  const graphWidth = bounds.maxX - bounds.minX + 120;
  const graphHeight = bounds.maxY - bounds.minY + 120;
  const scale = Math.min(1, width / graphWidth, height / graphHeight);
  const offsetX = width / 2 - ((bounds.minX + bounds.maxX) / 2) * scale;
  const offsetY = height / 2 - ((bounds.minY + bounds.maxY) / 2) * scale;

  return { layoutNodes, links, scale, offsetX, offsetY };
}

function drawGrid(ctx, width, height) {
  ctx.fillStyle = '#fbfdff';
  ctx.fillRect(0, 0, width, height);
  ctx.strokeStyle = 'rgba(64, 158, 255, 0.08)';
  ctx.lineWidth = 1;

  for (let x = 0; x < width; x += 28) {
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, height);
    ctx.stroke();
  }

  for (let y = 0; y < height; y += 28) {
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

function toScreen(node, layout) {
  return {
    x: node.x * layout.scale + layout.offsetX,
    y: node.y * layout.scale + layout.offsetY,
    width: node.width * layout.scale,
    height: node.height * layout.scale
  };
}

function drawLink(ctx, from, to, direction, layout) {
  const source = toScreen(from, layout);
  const target = toScreen(to, layout);
  const startX = source.x + (direction * source.width) / 2;
  const endX = target.x - (direction * target.width) / 2;
  const controlGap = Math.max(70 * layout.scale, Math.abs(endX - startX) / 2);

  ctx.beginPath();
  ctx.moveTo(startX, source.y);
  ctx.bezierCurveTo(startX + direction * controlGap, source.y, endX - direction * controlGap, target.y, endX, target.y);
  ctx.strokeStyle = '#8ab7d8';
  ctx.lineWidth = Math.max(1.5, 2.4 * layout.scale);
  ctx.stroke();
}

function drawNode(ctx, node, layout) {
  const box = toScreen(node, layout);
  const left = box.x - box.width / 2;
  const top = box.y - box.height / 2;
  const selected = node.id === props.selectedNodeId;
  const hovering = node.id === hoverNodeId.value;

  ctx.save();
  ctx.shadowColor = node.isRoot ? 'rgba(44, 85, 120, 0.16)' : 'rgba(40, 55, 70, 0.1)';
  ctx.shadowBlur = node.isRoot ? 18 : 10;
  ctx.shadowOffsetY = node.isRoot ? 8 : 5;
  roundedRect(ctx, left, top, box.width, box.height, 8);
  ctx.fillStyle = node.isRoot ? '#ffffff' : selected ? '#ecf5ff' : '#ffffff';
  ctx.fill();
  ctx.shadowColor = 'transparent';
  ctx.lineWidth = selected || hovering ? 2.4 : 1.4;
  ctx.strokeStyle = node.isRoot ? '#78b6e8' : selected || hovering ? '#409eff' : '#d5e1ec';
  ctx.stroke();

  ctx.fillStyle = node.isRoot ? '#214f77' : '#25384a';
  ctx.font = `${node.isRoot ? 700 : 600} ${Math.max(12, 14 * layout.scale)}px "Microsoft YaHei", "Segoe UI", Arial`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';

  const lineHeight = 18 * layout.scale;
  const startY = box.y - ((node.lines.length - 1) * lineHeight) / 2;
  node.lines.forEach((line, index) => {
    const suffix = index === 2 && String(node.title).length > line.length ? '...' : '';
    ctx.fillText(`${line}${suffix}`, box.x, startY + index * lineHeight);
  });

  ctx.restore();

  if (!node.isRoot) {
    hitRegions.value.push({ id: node.id, node, left, top, right: left + box.width, bottom: top + box.height });
  }
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
  drawGrid(ctx, width, height);

  if (!props.map || props.nodes.length === 0) {
    drawEmpty(ctx, width, height);
    return;
  }

  ctx.font = '14px "Microsoft YaHei", "Segoe UI", Arial';
  const layout = createLayout(ctx, width, height);
  layout.links.forEach((link) => drawLink(ctx, link.from, link.to, link.direction, layout));
  layout.layoutNodes.forEach((node) => drawNode(ctx, node, layout));
}

function findHit(event) {
  const canvas = canvasRef.value;
  if (!canvas) {
    return null;
  }

  const rect = canvas.getBoundingClientRect();
  const point = {
    x: event.clientX - rect.left,
    y: event.clientY - rect.top
  };

  return hitRegions.value.find((region) => {
    return point.x >= region.left && point.x <= region.right && point.y >= region.top && point.y <= region.bottom;
  }) ?? null;
}

function handlePointerMove(event) {
  const hit = findHit(event);
  const nextHoverId = hit?.id ?? null;
  if (nextHoverId !== hoverNodeId.value) {
    hoverNodeId.value = nextHoverId;
    scheduleDraw();
  }
}

function handlePointerLeave() {
  hoverNodeId.value = null;
  scheduleDraw();
}

function handleClick(event) {
  const hit = findHit(event);
  if (!hit) {
    return;
  }

  emit('select-node', hit.id);
  if (props.previewOnClick) {
    emit('preview-node', hit.node);
  }
}

watch(() => [props.map, props.nodes, props.selectedNodeId], () => scheduleDraw(), { deep: true });

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
  <section class="canvas-panel">
    <div class="section-heading">
      <h2>{{ map?.title ?? '未选择导图' }}</h2>
      <span>{{ nodes.length }} 个节点</span>
    </div>
    <div ref="wrapRef" class="mind-map-canvas-wrap" data-testid="mind-map-canvas">
      <canvas
        ref="canvasRef"
        aria-label="思维导图画布"
        @click="handleClick"
        @pointermove="handlePointerMove"
        @pointerleave="handlePointerLeave"
      />
    </div>
  </section>
</template>
