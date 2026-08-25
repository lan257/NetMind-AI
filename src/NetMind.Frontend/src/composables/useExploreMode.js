import { computed, ref } from 'vue';
import { getNodeExplore } from '../services/api';
import {
  backToExplorePath,
  clampExploreDepth,
  pushExplorePath
} from '../utils/explorePath';

/**
 * 知识探索模式组合式状态。
 * 全部状态使用 ref，可在组件外创建使用；支持沿路径逐层深入与回退。
 * 探索路径为临时 UI 状态，不持久化。
 */
export function useExploreMode() {
  /** 探索面板是否激活 */
  const exploreActive = ref(false);
  /** 探索路径（节点对象数组，尾节点为当前中心） */
  const explorePath = ref([]);
  /** 探索深度（1~3，默认 2） */
  const exploreDepth = ref(2);
  /** 是否正在请求探索数据 */
  const exploreLoading = ref(false);
  /** 探索错误信息 */
  const exploreError = ref(null);
  /** 最近一次成功返回的探索数据 { centerNode, nodes, relations } */
  const exploreData = ref(null);

  /** 请求序号，用于丢弃过期响应（防连点竞态） */
  let fetchSeq = 0;

  /** 按路径尾节点 + 深度发起探索请求；loading 期间新请求直接取代旧请求 */
  async function runExplore() {
    const tail = explorePath.value[explorePath.value.length - 1];
    if (!tail) return;
    const seq = ++fetchSeq;
    exploreLoading.value = true;
    try {
      const data = await getNodeExplore(tail.id, exploreDepth.value);
      if (seq !== fetchSeq) return; // 已被更新的请求取代
      exploreData.value = data;
      exploreError.value = null;
    } catch (err) {
      if (seq !== fetchSeq) return;
      exploreError.value = `探索失败：${err?.message || String(err)}`;
      // 保留旧 exploreData：UI 通过 exploreError 区分“失败”与“无数据”
    } finally {
      if (seq === fetchSeq) exploreLoading.value = false;
    }
  }

  /** 进入探索模式：以 node 为起点，重置深度并立即拉取 */
  function enterExploreMode(node) {
    exploreActive.value = true;
    explorePath.value = [node];
    exploreDepth.value = 2;
    exploreError.value = null;
    exploreData.value = null;
    runExplore();
  }

  /** 切换探索深度；值与当前相同则忽略，变化后重新探索当前中心节点 */
  function setExploreDepth(depth) {
    const clamped = clampExploreDepth(depth, 2);
    if (clamped === exploreDepth.value) return;
    exploreDepth.value = clamped;
    runExplore();
  }

  /** 点击探索结果中的节点：追加到路径并继续深入 */
  function exploreNodeClick(node) {
    const next = pushExplorePath(explorePath.value, node);
    if (next === explorePath.value) return; // 重复点击当前中心，忽略
    explorePath.value = next;
    exploreError.value = null;
    runExplore();
  }

  /** 通过面包屑回退到指定路径下标 */
  function goBackToPathIndex(index) {
    const truncated = backToExplorePath(explorePath.value, index);
    const prevTail = explorePath.value[explorePath.value.length - 1];
    const nextTail = truncated[truncated.length - 1];
    if (prevTail?.id === nextTail?.id) return; // 目标与当前尾节点相同（点了当前项），不重复请求
    explorePath.value = truncated;
    exploreError.value = null;
    runExplore();
  }

  /** 退出探索模式：复位全部状态并使在途请求失效 */
  function exitExploreMode() {
    fetchSeq++;
    exploreActive.value = false;
    explorePath.value = [];
    exploreDepth.value = 2;
    exploreLoading.value = false;
    exploreError.value = null;
    exploreData.value = null;
  }

  /** 当前中心节点：优先取后端返回的 centerNode，否则用路径尾节点兜底 */
  const exploreCenter = computed(() => {
    const tail = explorePath.value[explorePath.value.length - 1];
    return exploreData.value?.centerNode ?? tail ?? null;
  });

  /** 探索结果邻居节点（后端返回的 nodes，含中心节点） */
  const exploreNeighbors = computed(() => exploreData.value?.nodes ?? []);
  /** 探索结果关系边（后端返回的 relations） */
  const exploreRelations = computed(() => exploreData.value?.relations ?? []);

  return {
    exploreActive,
    explorePath,
    exploreDepth,
    exploreLoading,
    exploreError,
    exploreData,
    exploreCenter,
    exploreNeighbors,
    exploreRelations,
    enterExploreMode,
    setExploreDepth,
    exploreNodeClick,
    goBackToPathIndex,
    exitExploreMode
  };
}