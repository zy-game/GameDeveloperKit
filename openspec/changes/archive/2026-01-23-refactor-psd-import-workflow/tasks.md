# Tasks: 重构 PSD 导入工作流

## 1. 核心组件实现

- [x] 1.1 创建 `PsdDocumentBinding` 组件
  - [x] 定义 `LayerBinding` 数据结构（图层ID、名称、GameObject路径）
  - [x] 实现图层查找方法 `FindGameObject(layerId)`
  - [x] 实现绑定更新方法 `UpdateBinding()`
  - [x] 添加 PSD 文件哈希计算（用于检测源文件变化）

- [x] 1.2 创建 `PsdDocumentBindingEditor` Inspector 面板
  - [x] 显示 PSD 文件信息（路径、尺寸、图层数）
  - [x] 显示图层绑定列表（可折叠）
  - [x] 支持手动重新绑定图层
  - [x] 添加"重新导入"按钮

## 2. 菜单入口实现

- [x] 2.1 创建 `PsdImportMenu` 菜单类
  - [x] 添加 `GameDeveloperKit/导入 PSD` 菜单项
  - [x] 打开文件选择对话框
  - [x] 调用导入流程

- [x] 2.2 实现导入流程
  - [x] 使用 `PsdToUguiSettings` 全局设置
  - [x] 检测目标 Prefab 是否存在
  - [x] 根据情况选择首次导入或增量导入

## 3. 导入逻辑重构

- [x] 3.1 重构 `UguiConverter`
  - [x] 添加 `PsdDocumentBinding` 组件创建逻辑
  - [x] 实现首次导入流程（完整生成）
  - [x] 实现增量导入流程（智能合并）

- [x] 3.2 实现增量更新逻辑
  - [x] 图层内容更新（纹理、文本）
  - [x] 新增图层处理（添加到正确位置）
  - [x] 孤立图层标记（PSD 中已删除的图层）
  - [x] 保留用户添加的组件和脚本

## 4. 清理旧代码

- [x] 4.1 删除编辑器窗口相关代码
  - [x] 删除 `PsdToUguiEditorWindow.cs`
  - [x] 删除 `LayerTreeView.cs`
  - [x] 删除 `PreviewPanel.cs`
  - [x] 删除 `InspectorPanel.cs`
  - [x] 删除 `PsdEditStateManager.cs`

- [x] 4.2 清理相关引用
  - [x] 更新菜单项（移除旧的编辑器窗口入口）
  - [x] 创建 `AnchorUtils.cs` 保留锚点计算方法

## 5. 测试

- [x] 5.1 编译测试
  - [x] Unity 编译通过，无错误
