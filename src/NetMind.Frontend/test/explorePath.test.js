import assert from 'node:assert/strict';
import test from 'node:test';

import {
  backToExplorePath,
  clampExploreDepth,
  currentExploreNode,
  pushExplorePath
} from '../src/utils/explorePath.js';

test('clampExploreDepth 仅允许 1~3，非法值返回 fallback', () => {
  assert.equal(clampExploreDepth(2), 2);
  assert.equal(clampExploreDepth('3'), 3);
  assert.equal(clampExploreDepth(1), 1);
  assert.equal(clampExploreDepth(0), 2);
  assert.equal(clampExploreDepth(4), 2);
  assert.equal(clampExploreDepth(NaN), 2);
  assert.equal(clampExploreDepth(undefined), 2);
  assert.equal(clampExploreDepth(null, 3), 3); // 自定义 fallback
  assert.equal(clampExploreDepth('abc', 3), 3);
});

test('pushExplorePath 正常追加，重复点击同 id 不增长', () => {
  const a = { id: 1, title: 'A' };
  const b = { id: 2, title: 'B' };
  const path = [a];

  const next = pushExplorePath(path, b);
  assert.deepEqual(next, [a, b]);
  assert.notEqual(next, path); // 追加返回新数组

  // 重复点击尾节点同 id：原样返回，不增长
  assert.equal(pushExplorePath(next, { id: 2, title: 'B 新标题' }), next);
});

test('backToExplorePath 按 index 截断', () => {
  const a = { id: 1, title: 'A' };
  const b = { id: 2, title: 'B' };
  const d = { id: 4, title: 'D' };
  const path = [a, b, d];

  // 点 B(index=1) → [A, B]
  assert.deepEqual(backToExplorePath(path, 1), [a, b]);
  // index=0 → [A]
  assert.deepEqual(backToExplorePath(path, 0), [a]);
  // 负数视为 0 → [A]
  assert.deepEqual(backToExplorePath(path, -1), [a]);
  // 越界（>= length）→ 至少保留首节点 [A]
  assert.deepEqual(backToExplorePath(path, 99), [a]);
  // 空路径 → 空数组
  assert.deepEqual(backToExplorePath([], 0), []);
});

test('currentExploreNode 返回尾节点，空路径返回 null', () => {
  assert.equal(currentExploreNode([]), null);
  assert.equal(currentExploreNode([{ id: 1 }, { id: 2 }]).id, 2);
});