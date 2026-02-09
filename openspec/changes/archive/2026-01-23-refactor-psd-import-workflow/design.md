# Design: PSD 导入工作流重构

## Context

当前实现使用 `PsdEditStateManager` 将编辑状态保存到 `Library/PsdToUgui/` 目录的 JSON 文件中，并通过复杂的编辑器窗口进行配置。这种方式存在以下问题：

1. 状态与 Prefab 分离，容易丢失或不同步
2. 无法追踪 Prefab 的手动修改
3. 代码复杂度高，维护困难
4. 编辑器窗口功能繁琐，实际使用中大部分配置不需要

## Goals / Non-Goals

**Goals:**
- 简化 PSD 导入工作流（一键导入）
- 支持 Prefab 的增量更新，保留用户修改
- 将图层映射信息持久化到 Prefab 中
- 大幅减少代码复杂度

**Non-Goals:**
- 不改变 PSD 解析逻辑（`PsdParser`）
- 不改变纹理导出逻辑（`TextureExporter`）
- 不支持多个 PSD 文件合并到同一个 Prefab
- 不提供图层预览和实时编辑功能（直接在 Prefab 中编辑）

## Decisions

### Decision 1: 菜单入口设计

```csharp
public static class PsdImportMenu
{
    [MenuItem("GameDeveloperKit/导入 PSD")]
    public static void ImportPsd()
    {
        var settings = PsdToUguiSettings.Instance;
        var path = EditorUtility.OpenFilePanel("选择 PSD 文件", 
            settings.LastImportPath, "psd");
        
        if (string.IsNullOrEmpty(path)) return;
        
        settings.LastImportPath = Path.GetDirectoryName(path);
        settings.Save();
        
        PsdImporter.Import(path, settings);
    }
}
```

**Rationale:**
- 使用全局设置 `PsdToUguiSettings` 作为配置源
- 导出路径自动根据 PSD 文件名生成（`ExportRootPath/PsdName/`）
- 无需额外配置窗口

### Decision 2: PsdDocumentBinding 组件设计

```csharp
/// <summary>
/// PSD 文档绑定组件 - 记录 PSD 图层与 Prefab 节点的映射关系
/// </summary>
[DisallowMultipleComponent]
public class PsdDocumentBinding : MonoBehaviour
{
    [Serializable]
    public class LayerBinding
    {
        public int LayerId;           // PSD 图层 ID
        public string LayerName;      // PSD 图层名称（用于显示）
        public string GameObjectPath; // 相对于根节点的路径
        public int LayerType;         // 图层类型
    }
    
    [SerializeField] private string _psdFilePath;
    [SerializeField] private string _psdFileHash;  // 用于检测 PSD 是否修改
    [SerializeField] private int _psdWidth;
    [SerializeField] private int _psdHeight;
    [SerializeField] private List<LayerBinding> _bindings = new();
    
    // 运行时查找
    public GameObject FindGameObject(int layerId);
    public LayerBinding FindBinding(int layerId);
    public void UpdateBinding(int layerId, string newPath);
}
```

**Rationale:** 
- 使用 `MonoBehaviour` 而非 `ScriptableObject`，因为需要直接挂载到 Prefab
- 使用相对路径而非直接引用，避免 Prefab 内部引用问题
- 保存 PSD 文件哈希用于检测源文件变化
- 不再保存图层配置（锚点、9宫格等），这些直接在 Prefab 的组件上设置

### Decision 3: 增量导入策略

```
导入流程:
1. 解析 PSD 文件
2. 根据 PsdToUguiSettings 计算目标 Prefab 路径
3. 检查目标 Prefab 是否存在
   ├─ 不存在 → 首次导入流程
   └─ 存在 → 检查是否有 PsdDocumentBinding
       ├─ 没有 → 警告用户，询问是否覆盖
       └─ 有 → 增量导入流程

首次导入流程:
1. 按 PSD 图层树创建 GameObject 层级
2. 添加 PsdDocumentBinding 组件
3. 记录所有图层绑定
4. 保存 Prefab

增量导入流程:
1. 读取现有 PsdDocumentBinding
2. 对比 PSD 图层与绑定记录:
   - 匹配的图层 → 更新内容（纹理/文本），保留其他组件
   - 新增的图层 → 创建新节点，添加到正确位置
   - 删除的图层 → 标记为孤立，不自动删除
3. 更新 PsdDocumentBinding
4. 保存 Prefab
```

**Rationale:**
- 不自动删除孤立节点，因为用户可能故意保留某些元素
- 只更新 PSD 相关内容，保留用户添加的脚本和组件

### Decision 4: 移除编辑器窗口

完全移除以下文件：
- `PsdToUguiEditorWindow.cs`
- `LayerTreeView.cs`
- `PreviewPanel.cs`
- `InspectorPanel.cs`
- `PsdEditStateManager.cs`

**Rationale:**
- 编辑器窗口功能复杂但实际使用率低
- 图层配置（锚点、布局等）可以直接在 Prefab 的 Inspector 中设置
- 减少约 2000+ 行代码

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| 失去预览功能 | 直接在 Scene 中预览 Prefab |
| 失去批量配置功能 | 可以在 Prefab 中使用多选编辑 |
| 旧 Prefab 没有 PsdDocumentBinding | 重新导入即可 |

## Migration Plan

1. **Phase 1**: 实现 `PsdDocumentBinding` 组件和菜单入口
2. **Phase 2**: 实现增量导入逻辑
3. **Phase 3**: 删除编辑器窗口相关代码
4. **Phase 4**: 测试和修复

## Open Questions

1. `PsdDocumentBinding` 是否应该放在 Runtime 程序集？
   - **建议**：放 Runtime，运行时基本无开销，且可以在运行时查询绑定信息（如果需要）

2. 是否保留全局设置窗口 `PsdToUguiSettingsWindow`？
   - **建议**：保留，用于配置导出路径、字体、纹理设置等
