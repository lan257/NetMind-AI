/**
 * 知识探索路径纯函数工具集。
 * 仅依赖基础 JS，可被 node:test 直接 import，不依赖 Vue / Element Plus。
 */

/**
 * 将深度值收敛为合法的探索深度（1~3）。
 * @param {*} value 输入的深度值（数字或数字字符串）
 * @param {number} [fallback=2] 非法值时的兜底深度
 * @returns {number} 合法的探索深度
 */
export function clampExploreDepth(value, fallback = 2) {
  const n = Number(value);
  if (Number.isInteger(n) && n >= 1 && n <= 3) {
    return n;
  }
  return fallback;
}

/**
 * 向探索路径追加节点；若尾节点与 node.id 相同则原样返回（防重复点击）。
 * @param {Array<object>} path 当前探索路径（节点对象数组）
 * @param {object} node 待追加的节点对象
 * @returns {Array<object>} 追加后的新路径；重复时返回原 path
 */
export function pushExplorePath(path, node) {
  const tail = path[path.length - 1];
  if (tail && tail.id === node.id) {
    return path;
  }
  return [...path, node];
}

/**
 * 截断探索路径至指定下标（含），index<0 视为 0；越界或非法值防御性保留首节点。
 * @param {Array<object>} path 当前探索路径
 * @param {number} index 目标下标（对应面包屑点击位置）
 * @returns {Array<object>} 截断后的路径，至少保留首节点
 */
export function backToExplorePath(path, index) {
  if (!path.length) return [];
  const i = Number(index);
  if (!Number.isInteger(i) || i < 0 || i >= path.length) {
    return path.slice(0, 1);
  }
  return path.slice(0, i + 1);
}

/**
 * 获取探索路径的当前节点（尾节点）。
 * @param {Array<object>} path 当前探索路径
 * @returns {object|null} 尾节点；空路径返回 null
 */
export function currentExploreNode(path) {
  return path.length ? path[path.length - 1] : null;
}