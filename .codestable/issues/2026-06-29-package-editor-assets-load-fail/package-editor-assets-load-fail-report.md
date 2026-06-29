---
doc_type: issue-report
issue: 2026-06-29-package-editor-assets-load-fail
status: confirmed
severity: P0
summary: GameDeveloperKit 作为 file 包引用到其他 Unity 工程后编辑器窗口资源加载失败，UnityBridge 样式丢失且布局混乱
tags:
  - unity-editor
  - package
  - editor-assets
---

# Package Editor Assets Load Fail Issue Report

## 1. 问题现象

GameDeveloperKit 通过 `file:` 方式作为 Unity package 被其他 Unity 工程引用后，打开框架内编辑器窗口会出现编辑器资源加载异常。已观察到的例子是打开 `UnityBridge` 后窗口布局混乱，样式全部丢失。当前判断框架中所有编辑器工具都可能存在相同现象。

## 2. 复现步骤

1. 新建或打开一个独立 Unity 工程。
2. 在该工程的 `Packages/manifest.json` 中通过 `file:` 引用本地 GameDeveloperKit。
3. 等 Unity 导入和编译完成。
4. 通过菜单打开 `UnityBridge`。
5. 观察到：`UnityBridge` 窗口样式丢失，布局显示混乱。

复现频率：稳定。

## 3. 期望 vs 实际

**期望行为**：通过 `file:` 方式把 GameDeveloperKit 作为包引用到其他 Unity 工程后，打开 `UnityBridge` 及框架内其他编辑器工具时，应该和本仓库内打开时一样，正常加载编辑器资源、样式并显示完整布局。

**实际行为**：打开 `UnityBridge` 后窗口样式丢失，布局显示混乱；框架内其他编辑器工具可能也存在同类资源加载失败现象。

## 4. 环境信息

- 涉及模块 / 功能：UnityBridge 编辑器窗口，以及框架内其他依赖编辑器资源的 EditorWindow / 编辑器工具。
- 相关文件 / 函数：待定，阶段 2 分析时查。
- 运行环境：Unity Editor，其他 Unity 工程中通过 `file:` 本地包引用 GameDeveloperKit。
- 其他上下文：具体 Unity 版本和 `file:` 指向路径待补充。

## 5. 严重程度

**P0** — 框架以包形式被其他工程引用时，编辑器工具可能整体无法正常显示和使用，影响所有通过包方式集成框架的使用场景。

## 备注

已知示例：`UnityBridge` 窗口样式全部丢失、布局混乱。
