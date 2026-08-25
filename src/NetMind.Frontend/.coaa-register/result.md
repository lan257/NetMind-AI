# COAA worker - FINAL CHECKPOINT

STATUS: SUCCESS (验证限制见下)

## 变更
- 添加 src/services/api.js -> getNodeExplore(id, depth)
- 新增 src/utils/explorePath.js: clampExploreDepth/pushExplorePath/backToExplorePath/currentExploreNode
- 新增 src/composables/useExploreMode.js: 全 ref 状态 + fetch 防竞态(seq)
- 新增 src/components/ExplorePanel.vue: el-dialog, 面包屑, 深度radio, 图谱复用 RelationGraphCanvas(preview-node 事件), 空态/错误/loading
- 修改 src/components/KnowledgeCard.vue: Search 图标 + '知识探索' 按钮 emit('explore', currentNode) + defineEmits 增加 explore
- 修改 src/App.vue: useExploreMode + ExplorePanel v-model 接线 + watch 同步复位
- 新增 test/explorePath.test.js (4 用例, 未动现有测试)

## 验证（真实）
- npm run build: 失败 spawn EPERM —— esbuild 原生服务需 spawn 子进程，沙箱禁止 spawn（环境问题，非代码）。备选: SFC 编译检查（@vue/compiler-sfc）4/4 OK；组合式冒烟测试 15/15 PASS（见 artifacts/verify.mjs）
- npm test: 失败 spawn EPERM —— node --test 每文件 spawn 子进程被沙箱阻止（现有 test/useMarkdown.test.js 同样失败，非回归）。备选: node --test --test-isolation=none => 6/6 PASS；node test/explorePath.test.js 直接运行 => 4/4 PASS
- 关键事实: api.js 实际签名为 api(path, options)（fetch 风格，返回 data，失败 throw），非任务描述的 api(method,url,data)；getNodeExplore 已按实际签名实现。RelationGraphCanvas 已有 preview-node 点击事件，无需改动。
