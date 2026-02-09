## Context
GameDeveloperKit 需要一套战斗系统来支持游戏中的技能、属性、效果等核心战斗玩法。参考 Unreal Engine 的 GAS (Gameplay Ability System) 设计，但针对 Unity 和本框架的特点进行简化和适配。

## Goals / Non-Goals
**Goals:**
- 提供完整的属性系统（基础值、修改器、最终值计算）
- 提供技能系统（技能定义、释放、冷却、消耗）
- 提供效果系统（即时效果、持续效果、周期效果）
- 提供标签系统（状态标记、条件判断）
- 提供伤害系统（伤害计算、伤害类型）
- 与现有 ECS 架构兼容
- 支持数据驱动配置

**Non-Goals:**
- 不实现完整的 GAS 预测/回滚系统（可后续扩展）
- 不实现可视化技能编辑器（可后续扩展）
- 不实现 AI 行为树集成（独立模块）

## Architecture Overview

```
Combat Module
├── Core
│   ├── CombatModule.cs          // 模块入口
│   ├── ICombatManager.cs        // 管理器接口
│   └── CombatEntity.cs          // 战斗实体包装
├── Attributes
│   ├── AttributeSet.cs          // 属性集合
│   ├── AttributeModifier.cs     // 属性修改器
│   ├── AttributeValue.cs        // 属性值（基础值+当前值）
│   └── IAttributeOwner.cs       // 属性持有者接口
├── Abilities
│   ├── AbilityBase.cs           // 技能基类
│   ├── AbilitySpec.cs           // 技能实例规格
│   ├── AbilitySystemComponent.cs // 技能系统组件
│   ├── IAbilityOwner.cs         // 技能持有者接口
│   └── AbilityCost.cs           // 技能消耗定义
├── Effects
│   ├── GameplayEffect.cs        // 游戏效果定义
│   ├── EffectSpec.cs            // 效果实例规格
│   ├── EffectModifier.cs        // 效果修改器
│   ├── EffectPolicy.cs          // 堆叠策略
│   └── ActiveEffect.cs          // 激活中的效果
├── Tags
│   ├── GameplayTag.cs           // 游戏标签
│   ├── TagContainer.cs          // 标签容器
│   └── TagQuery.cs              // 标签查询
├── Cues
│   ├── GameplayCue.cs           // 游戏表现
│   ├── CueNotify.cs             // 表现通知
│   └── ICueHandler.cs           // 表现处理器接口
├── Damage
│   ├── DamageInfo.cs            // 伤害信息
│   ├── DamageType.cs            // 伤害类型
│   ├── DamageCalculator.cs      // 伤害计算器
│   └── IDamageReceiver.cs       // 伤害接收者接口
└── Events
    ├── CombatEventArgs.cs       // 战斗事件基类
    ├── DamageEventArgs.cs       // 伤害事件
    ├── AbilityEventArgs.cs      // 技能事件
    └── EffectEventArgs.cs       // 效果事件
```

## Decisions

### 决策 1: 属性系统设计
**选择**: 使用 AttributeSet + AttributeModifier 模式

**理由**:
- AttributeSet 定义一组相关属性（如 HealthSet: HP, MaxHP, HPRegen）
- AttributeModifier 支持加法、乘法、覆盖三种修改方式
- 修改器支持优先级排序
- 与 GAS 设计一致，便于理解

**属性计算公式**:
```
FinalValue = (BaseValue + FlatAdd) * (1 + PercentAdd) * PercentMul
```

### 决策 2: 技能系统设计
**选择**: AbilityBase (定义) + AbilitySpec (实例) 分离

**理由**:
- AbilityBase 是 ScriptableObject，定义技能的静态数据
- AbilitySpec 是运行时实例，包含冷却、等级等动态数据
- 支持同一技能定义创建多个实例（如不同等级）

**技能生命周期**:
```
CanActivate -> PreActivate -> Activate -> Execute -> End -> PostEnd
```

### 决策 3: 效果系统设计
**选择**: GameplayEffect (定义) + ActiveEffect (实例) 模式

**效果类型**:
- Instant: 即时效果，立即应用并结束
- Duration: 持续效果，有持续时间
- Infinite: 无限效果，需手动移除
- Periodic: 周期效果，定时触发

**堆叠策略**:
- None: 不堆叠，忽略新效果
- Refresh: 刷新持续时间
- Stack: 堆叠层数（有上限）
- Override: 覆盖旧效果

### 决策 4: 标签系统设计
**选择**: 层级标签 + 容器查询

**标签格式**: `Category.SubCategory.Name` (如 `State.Buff.Invincible`)

**查询类型**:
- HasTag: 精确匹配
- HasTagExact: 完全匹配
- HasAnyTag: 任意匹配
- HasAllTags: 全部匹配

### 决策 5: 与 ECS 集成
**选择**: 提供 ECS 组件包装，但核心逻辑独立

**理由**:
- CombatComponent 作为 ECS 组件附加到实体
- 核心战斗逻辑不依赖 ECS，可独立使用
- 通过 System 驱动战斗更新

## Data Structures

### AttributeValue
```csharp
public struct AttributeValue
{
    public float BaseValue;      // 基础值
    public float CurrentValue;   // 当前值（计算后）
    public float MinValue;       // 最小值
    public float MaxValue;       // 最大值
}
```

### AttributeModifier
```csharp
public struct AttributeModifier
{
    public ModifierOp Operation; // Add, Multiply, Override
    public float Value;
    public int Priority;
    public object Source;        // 修改来源
}
```

### GameplayTag
```csharp
public readonly struct GameplayTag : IEquatable<GameplayTag>
{
    public readonly int Id;      // 哈希ID，用于快速比较
    public readonly string Name; // 完整标签名
}
```

### DamageInfo
```csharp
public struct DamageInfo
{
    public float BaseDamage;
    public DamageType Type;
    public object Source;        // 伤害来源
    public object Causer;        // 造成者（技能/效果）
    public bool IsCritical;
    public float CritMultiplier;
    public TagContainer Tags;    // 伤害标签
}
```

## Risks / Trade-offs
- **风险**: 系统复杂度较高
  - **缓解**: 提供简化的快捷API，复杂功能按需使用
- **风险**: 性能开销
  - **缓解**: 使用对象池、避免GC、批量处理
- **风险**: 学习曲线
  - **缓解**: 提供示例和文档

## Migration Plan
1. 实现核心模块结构
2. 实现属性系统
3. 实现标签系统
4. 实现效果系统
5. 实现技能系统
6. 实现伤害系统
7. 实现表现系统
8. 集成测试和示例

## Open Questions
- 是否需要支持技能的网络同步预测？
- 是否需要提供可视化的技能/效果编辑器？
- 属性集是否需要支持运行时动态添加属性？
