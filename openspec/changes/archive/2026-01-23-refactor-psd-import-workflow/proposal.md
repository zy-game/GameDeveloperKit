# Change: 重构 PSD 导入工作流 - 基于 Prefab 的增量更新方案

## Why

当前 PSD 转 UGUI 工具存在以下问题：

1. **工作流繁琐**：需要打开编辑器窗口，手动配置图层属性（锚点、布局、9宫格等），配置信息保存在 `Library/PsdToUgui/` 的 JSON 文件中
2. **维护困难**：编辑状态与 Prefab 分离，状态管理代码复杂（`PsdEditStateManager` 约 300 行）
3. **无法追踪 Prefab 修改**：导出 Prefab 后，如果手动修改了 Prefab（添加脚本、调整布局），重新导入会覆盖这些修改
4. **图层映射丢失**：PSD 图层 ID 与 Prefab 节点之间没有持久化的映射关系

## What Changes

### 核心改动

1. **新增菜单入口**
   - `GameDeveloperKit/导入 PSD` - 选择 PSD 文件直接导入
   - 使用 `PsdToUguiSettings` 全局设置作为配置（导出路径、字体、纹理设置等）
   - 不再需要编辑器窗口

2. **新增 `PsdDocumentBinding` 运行时组件**
   - 挂载到 Prefab 根节点
   - 记录 PSD 文件路径、图层 ID 到 GameObject 路径的映射
   - 支持序列化，随 Prefab 一起保存

3. **重构导入流程**
   - **首次导入**：按 PSD 图层树生成 Prefab，自动添加 `PsdDocumentBinding` 组件
   - **增量导入**：检测到已有 `PsdDocumentBinding` 时，进入智能合并模式
     - 根据图层 ID 匹配现有节点
     - 只更新图层内容（纹理、文本），保留用户添加的组件和脚本
     - 新增图层自动添加到对应位置
     - 删除的图层标记为"孤立节点"，由用户决定是否删除

4. **新增图层绑定 Inspector 面板**
   - 在 Inspector 中显示 `PsdDocumentBinding` 组件
   - 可视化图层映射关系
   - 支持手动重新绑定图层
   - 提供"重新导入"按钮

### 删除的代码

- `PsdToUguiEditorWindow.cs` - 不再需要编辑器窗口
- `PsdEditStateManager.cs` - 状态管理逻辑移入 `PsdDocumentBinding`
- `LayerTreeView.cs`, `PreviewPanel.cs`, `InspectorPanel.cs` - 编辑器窗口相关组件
- `Library/PsdToUgui/*.json` - 不再需要外部状态文件

## Impact

- **Affected specs**: `psd-to-ugui`
- **Affected code**:
  - `Assets/Editor/PsdToUgui/` - 重构导入逻辑，删除编辑器窗口
  - `Assets/Runtime/PsdToUgui/` - 新增运行时组件
- **Breaking changes**: 
  - 旧版导出的 Prefab 需要重新导入以添加 `PsdDocumentBinding`
  - `Library/PsdToUgui/` 中的状态文件将不再使用
  - 编辑器窗口将被移除
