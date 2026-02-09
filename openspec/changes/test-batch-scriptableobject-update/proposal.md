# Change: 测试批量更新 ScriptableObject

## Why
验证 Unity MCP 工具是否支持并行批量更新多个 ScriptableObject 资源，测试同时更新 10 个资源的性能和可靠性。

## What Changes
- 同时调用 10 次 `unity_update_scriptable_object` 工具
- 更新 TestAbility_01 到 TestAbility_10 的字段值
- 验证所有更新是否成功完成

## Impact
- Affected specs: 无（仅测试）
- Affected code: Assets/Data/Abilities/TestAbility_*.asset

## Test Plan
1. 并行调用 10 次更新工具
2. 每个资源更新不同的 AbilityName 和 Cooldown 值
3. 验证更新结果
