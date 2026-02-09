## Context
GameDeveloperKit 的 MCP 服务需要在两种部署模式下正常工作：
1. **Assets 模式**（开发）：代码位于 `Assets/Editor/MCPService/`
2. **Packages 模式**（发布）：代码位于 `Packages/com.gamedeveloperkit.framework/Editor/MCPService/`

当前实现使用硬编码的 `Application.dataPath` 路径，在包模式下无法正确定位资源文件。

## Goals / Non-Goals

**Goals:**
- 支持 Assets 和 Packages 两种部署模式
- 扩展工具集覆盖更多常用 Unity 资源类型
- 保持向后兼容，现有工具行为不变
- 提供安全的代码执行能力

**Non-Goals:**
- 不支持运行时（非编辑器）MCP 服务
- 不支持远程 Unity 实例连接
- 不实现完整的 C# 解释器（仅支持简单表达式）

## Decisions

### 1. 包路径解析策略

**Decision:** 使用 `PackageInfo.FindForAssembly()` API 动态检测包安装位置

**Rationale:**
- Unity 官方推荐的包路径解析方式
- 自动处理 Assets 和 Packages 两种情况
- 无需维护多套路径配置

**Implementation:**
```csharp
private static string GetPackageRoot()
{
    var assembly = typeof(MCPServer).Assembly;
    var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
    
    if (packageInfo != null)
    {
        // 包模式：Packages/com.gamedeveloperkit.framework/
        return packageInfo.resolvedPath;
    }
    
    // Assets 模式：回退到 Application.dataPath
    return Path.Combine(Application.dataPath, "Editor/MCPService");
}
```

### 2. Handler 架构扩展

**Decision:** 保持现有 `IResourceHandler` 接口，新增 Handler 类

**Alternatives considered:**
- 合并到单一大 Handler：违反单一职责原则
- 使用反射自动发现 Handler：增加复杂性，调试困难

**New Handlers:**
- `MaterialHandler` - 材质操作
- `TextureHandler` - 纹理操作
- `AnimationHandler` - 动画操作
- `AudioHandler` - 音频操作
- `AssetHandler` - 通用资源操作
- `ConsoleHandler` - 控制台操作
- `EditorHandler` - 编辑器状态操作
- `CodeHandler` - 代码执行（沙箱）

### 3. 代码执行安全策略

**Decision:** 使用白名单 + 静态分析限制可执行代码

**Restrictions:**
- 禁止 `System.IO` 命名空间（文件操作）
- 禁止 `System.Net` 命名空间（网络操作）
- 禁止 `System.Diagnostics.Process`（进程操作）
- 禁止 `System.Reflection.Emit`（动态代码生成）
- 仅允许返回可序列化类型

**Implementation:** 使用 Roslyn 编译并在执行前进行语法树分析

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| 代码执行可能被滥用 | 严格的白名单限制 + 执行超时 |
| 新工具增加维护负担 | 统一的 Handler 模式 + 完善测试 |
| 包路径解析可能失败 | 提供明确的错误信息和回退机制 |

## Migration Plan

1. **Phase 1:** 修复包模式路径解析（无破坏性变更）
2. **Phase 2:** 添加新 Handler（增量添加，不影响现有功能）
3. **Phase 3:** 增强现有 Handler（向后兼容）

## Open Questions

1. 是否需要支持自定义 Handler 注册机制？（允许用户扩展工具）
2. 代码执行功能是否应该默认禁用，需要用户显式启用？
