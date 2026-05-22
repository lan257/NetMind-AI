import assert from 'node:assert/strict';
import test from 'node:test';

import { renderMarkdown } from '../src/composables/useMarkdown.js';

test('renders richer markdown blocks and inline styles', () => {
  const html = renderMarkdown([
    '1. 第一项',
    '2. 第二项',
    '',
    '--删除线--、~~删除线~~和*斜体*',
    '',
    '---',
    '',
    '> 这是浅色注释',
    '> 支持连续两行',
  ].join('\n'));

  assert.match(html, /<ol><li>第一项<\/li><li>第二项<\/li><\/ol>/);
  assert.match(html, /<del>删除线<\/del>、<del>删除线<\/del>和<em>斜体<\/em>/);
  assert.match(html, /<hr>/);
  assert.match(html, /<blockquote>这是浅色注释<br>支持连续两行<\/blockquote>/);
});

test('keeps ordered list start values and code span markdown literal', () => {
  const html = renderMarkdown([
    '3. 第三项',
    '4. 第四项',
    '',
    '`--不删除--`',
  ].join('\n'));

  assert.match(html, /<ol start="3"><li>第三项<\/li><li>第四项<\/li><\/ol>/);
  assert.match(html, /<p><code>--不删除--<\/code><\/p>/);
});
