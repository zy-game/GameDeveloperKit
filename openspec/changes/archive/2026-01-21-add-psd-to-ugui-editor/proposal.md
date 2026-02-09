# Change: 添加PSD转UGUI编辑器

## Why
当前美术设计师使用Photoshop设计UI界面后，程序员需要手动在Unity中重建UI层级结构，这个过程耗时且容易出错。需要一个自动化工具来读取PSD文件信息，直接转换成UGUI组件，大幅提升UI制作效率。

主要痛点：
- 手动创建UI层级结构耗时长
- 图层位置、大小需要手动调整
- 文本图层的字体、颜色、效果需要逐个配置
- 图层嵌套关系容易出错
- 修改设计后需要重新手动调整

## What Changes
- 添加 `PsdToUguiEditorWindow` 编辑器窗口，提供三栏布局（图层树、预览、属性面板）
- 实现PSD文件解析功能，读取图层信息（名称、位置、大小、类型、效果）
- 支持图层树显示和拖拽重组功能
- 区分文本图层和图片图层，自动创建对应的UGUI组件（Text/TextMeshPro 和 Image）
- 解析PSD文本图层效果（描边、阴影、渐变等）并映射到UGUI组件
- 提供预览窗口实时显示转换结果
- 提供属性面板编辑基础属性（锚点、位置、大小、颜色等）
- 支持导出为Prefab

## Impact
- 新增功能模块：`psd-to-ugui` capability
- 影响的代码区域：
  - `Assets/Editor/PsdToUgui/` - 新增编辑器窗口和相关工具
  - 需要引入PSD解析库（如 PSD-Parser 或 Ntreev.Library.Psd）
- 依赖项：
  - 第三方PSD解析库
  - Unity UI Toolkit (用于编辑器界面)
  - TextMeshPro (可选，用于高级文本渲染)
- 不影响现有模块和运行时代码
