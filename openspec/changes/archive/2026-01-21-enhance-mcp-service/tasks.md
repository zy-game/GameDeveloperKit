## 1. 包模式路径解析修复

- [x] 1.1 在 `MCPServer.cs` 中添加 `GetMCPServicePath()` 方法，使用 `PackageInfo.FindForAssembly()` 检测包位置
- [x] 1.2 更新 `_taskFilePath` 初始化逻辑，使用动态路径
- [x] 1.3 更新 `InstallProxy()` 方法，使用动态路径定位 `unity_mcp_proxy.py` 和 `install_proxy.ps1`
- [x] 1.4 更新 `CheckProxyInstalled()` 方法（如需要）
- [ ] 1.5 测试：在 Assets 模式下验证路径解析正确
- [ ] 1.6 测试：创建测试项目，以包形式引用 GameDeveloperKit，验证路径解析正确

## 2. 新增资源操作 Handler

- [x] 2.1 创建 `MaterialHandler.cs`，实现 list/create/get/update/delete 操作
- [x] 2.2 创建 `TextureHandler.cs`，实现 list/get/update 操作
- [x] 2.3 创建 `AnimationHandler.cs`，实现 list/get 操作
- [x] 2.4 创建 `AudioHandler.cs`，实现 list/get/update 操作
- [x] 2.5 创建 `AssetHandler.cs`，实现 copy/move/rename/find_references/refresh_assets 操作
- [x] 2.6 在 `MCPServer.RegisterHandlers()` 中注册新 Handler
- [ ] 2.7 测试：验证所有新工具在 MCP 客户端中可见

## 3. 新增编辑器辅助 Handler

- [x] 3.1 创建 `ConsoleHandler.cs`，实现 get_logs/clear/log 操作
- [x] 3.2 创建 `EditorHandler.cs`，实现 get_state/set_play_mode/set_pause/select_objects/step_frame/focus_gameobject 操作
- [ ] 3.3 创建 `CodeHandler.cs`，实现安全的代码执行功能（延后实现，需要更多安全考虑）
- [x] 3.4 在 `MCPServer.RegisterHandlers()` 中注册新 Handler
- [ ] 3.5 测试：验证控制台操作正常工作
- [ ] 3.6 测试：验证编辑器状态操作正常工作

## 4. 增强现有 Handler

- [x] 4.1 在 `GameObjectHandler.cs` 中添加 `unity_get_component` 和 `unity_set_component` 工具
- [x] 4.2 在 `PrefabHandler.cs` 中添加 `unity_instantiate_prefab`、`unity_apply_prefab_overrides`、`unity_revert_prefab_overrides` 工具
- [ ] 4.3 在 `ScriptableObjectHandler.cs` 中增强 `unity_update_scriptable_object` 支持嵌套字段和数组（延后实现）
- [ ] 4.4 测试：验证组件属性读写正常
- [ ] 4.5 测试：验证 Prefab 实例化和覆盖管理正常

## 5. 文档和技能更新

- [ ] 5.1 更新 `Assets/Editor/MCPService/TOOLS.md`，添加新工具文档
- [ ] 5.2 更新 `.factory/skills/unity-mcp.md`，添加新工具使用示例
- [ ] 5.3 更新 `AGENTS.md` 中的 MCP 工具列表

## 6. 验证和收尾

- [ ] 6.1 运行所有 MCP 工具的集成测试
- [ ] 6.2 在实际 AI 助手中测试新工具
- [ ] 6.3 更新 openspec 规范文档
