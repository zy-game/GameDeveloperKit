# Change: 添加战斗系统 (Combat System)

## Why
游戏框架缺少一套完善的战斗系统，无法支持技能释放、属性计算、效果应用、伤害处理等核心战斗玩法。参考 Unreal Engine 的 GAS (Gameplay Ability System) 设计理念，为框架提供一套灵活、可扩展的战斗系统。

## What Changes
- **ADDED** `Combat` 模块：战斗系统核心模块，管理战斗实体和战斗流程
- **ADDED** `Attribute` 系统：属性定义、修改器、属性计算
- **ADDED** `Ability` 系统：技能定义、技能释放、冷却管理、消耗检查
- **ADDED** `Effect` 系统：游戏效果（GameplayEffect）、持续效果、即时效果
- **ADDED** `Tag` 系统：标签管理、标签查询、标签条件
- **ADDED** `Cue` 系统：战斗表现（音效、特效、动画触发）
- **ADDED** `Damage` 系统：伤害计算、伤害类型、伤害事件

## Impact
- Affected specs: 新增 `combat` 能力规格
- Affected code: 
  - `Assets/Runtime/Combat/` - 新增战斗系统目录
  - `Assets/Runtime/Game.cs` - 添加 Combat 模块引用
  - `Assets/GameFrameworkKit.asmdef` - 添加程序集引用

## Design Principles (参考 GAS)
1. **数据驱动**: 技能、效果、属性通过配置定义，支持热更新
2. **组件化**: 战斗能力通过组件附加到实体
3. **标签系统**: 使用标签进行状态管理和条件判断
4. **效果堆叠**: 支持效果的堆叠、刷新、覆盖策略
5. **预测与回滚**: 为网络同步预留接口
