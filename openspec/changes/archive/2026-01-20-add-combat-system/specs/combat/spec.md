## ADDED Requirements

### Requirement: Combat Module
系统 SHALL 提供 `CombatModule` 作为战斗系统的核心模块，实现 `IModule` 接口，通过 `Game.Combat` 访问。

#### Scenario: 模块初始化
- **WHEN** 游戏框架启动
- **THEN** CombatModule 完成初始化
- **AND** 可通过 `Game.Combat` 访问战斗管理器

#### Scenario: 模块更新
- **WHEN** 每帧更新时
- **THEN** CombatModule 更新所有激活的效果和冷却

---

### Requirement: Attribute System
系统 SHALL 提供属性系统，支持属性定义、修改器、属性计算。

#### Scenario: 定义属性集
- **WHEN** 创建 AttributeSet 子类
- **THEN** 可定义一组相关属性（如 Health, MaxHealth, Attack）
- **AND** 每个属性包含 BaseValue 和 CurrentValue

#### Scenario: 应用属性修改器
- **WHEN** 添加 AttributeModifier 到属性
- **THEN** 属性的 CurrentValue 根据修改器重新计算
- **AND** 支持 Add、Multiply、Override 三种操作

#### Scenario: 移除属性修改器
- **WHEN** 移除 AttributeModifier
- **THEN** 属性的 CurrentValue 重新计算
- **AND** 不影响其他修改器

#### Scenario: 属性变化通知
- **WHEN** 属性的 CurrentValue 发生变化
- **THEN** 触发 OnAttributeChanged 事件
- **AND** 事件包含旧值和新值

---

### Requirement: Tag System
系统 SHALL 提供标签系统，支持层级标签、标签容器、标签查询。

#### Scenario: 创建标签
- **WHEN** 使用 `GameplayTag.Get("State.Buff.Invincible")`
- **THEN** 返回对应的 GameplayTag 实例
- **AND** 相同名称返回相同实例

#### Scenario: 标签容器操作
- **WHEN** 使用 TagContainer
- **THEN** 可添加、移除、查询标签
- **AND** 支持批量操作

#### Scenario: 标签查询
- **WHEN** 使用 TagQuery 查询
- **THEN** 支持 HasTag、HasAnyTag、HasAllTags 查询
- **AND** 支持层级匹配（父标签匹配子标签）

---

### Requirement: Effect System
系统 SHALL 提供效果系统，支持即时效果、持续效果、周期效果。

#### Scenario: 应用即时效果
- **WHEN** 应用 Instant 类型的 GameplayEffect
- **THEN** 立即执行效果逻辑
- **AND** 效果执行完毕后自动结束

#### Scenario: 应用持续效果
- **WHEN** 应用 Duration 类型的 GameplayEffect
- **THEN** 效果持续指定时间
- **AND** 时间结束后自动移除

#### Scenario: 应用周期效果
- **WHEN** 应用 Periodic 类型的 GameplayEffect
- **THEN** 按指定间隔重复执行效果
- **AND** 直到持续时间结束或手动移除

#### Scenario: 效果堆叠
- **WHEN** 重复应用相同效果
- **THEN** 根据堆叠策略处理（None/Refresh/Stack/Override）
- **AND** Stack 策略支持最大堆叠数限制

#### Scenario: 效果条件
- **WHEN** 效果定义了标签条件
- **THEN** 只有满足条件时效果才能应用
- **AND** 条件不满足时效果被阻止

---

### Requirement: Ability System
系统 SHALL 提供技能系统，支持技能定义、释放、冷却、消耗。

#### Scenario: 授予技能
- **WHEN** 调用 `GiveAbility(abilityDef)`
- **THEN** 创建 AbilitySpec 实例
- **AND** 技能可被激活

#### Scenario: 激活技能
- **WHEN** 调用 `TryActivateAbility(abilitySpec)`
- **THEN** 检查激活条件（冷却、消耗、标签）
- **AND** 条件满足时执行技能逻辑

#### Scenario: 技能冷却
- **WHEN** 技能执行完毕
- **THEN** 进入冷却状态
- **AND** 冷却期间无法再次激活

#### Scenario: 技能消耗
- **WHEN** 技能定义了消耗
- **THEN** 激活前检查资源是否足够
- **AND** 激活时扣除消耗

#### Scenario: 技能取消
- **WHEN** 调用 `CancelAbility(abilitySpec)`
- **THEN** 中断正在执行的技能
- **AND** 触发取消回调

---

### Requirement: Damage System
系统 SHALL 提供伤害系统，支持伤害计算、伤害类型、伤害事件。

#### Scenario: 计算伤害
- **WHEN** 调用 `DamageCalculator.Calculate(damageInfo, target)`
- **THEN** 根据攻击者属性、目标属性、伤害类型计算最终伤害
- **AND** 支持暴击、护甲、抗性计算

#### Scenario: 应用伤害
- **WHEN** 调用 `ApplyDamage(target, damageInfo)`
- **THEN** 目标的生命值减少
- **AND** 触发 OnDamageReceived 事件

#### Scenario: 伤害类型
- **WHEN** 定义伤害类型（Physical, Magical, True）
- **THEN** 不同类型使用不同的减伤公式
- **AND** 支持自定义伤害类型

---

### Requirement: Cue System
系统 SHALL 提供表现系统，支持战斗表现的触发和播放。

#### Scenario: 触发表现
- **WHEN** 效果或技能触发 GameplayCue
- **THEN** 通知注册的 ICueHandler
- **AND** Handler 执行对应的表现逻辑

#### Scenario: 表现类型
- **WHEN** 定义 GameplayCue
- **THEN** 支持 OnExecute（一次性）和 OnActive/OnRemove（持续性）
- **AND** 可关联音效、特效、动画

---

### Requirement: Combat Events
系统 SHALL 提供战斗事件，支持战斗流程的监听和响应。

#### Scenario: 伤害事件
- **WHEN** 实体受到伤害
- **THEN** 触发 DamageEventArgs 事件
- **AND** 事件包含伤害来源、目标、伤害值、伤害类型

#### Scenario: 技能事件
- **WHEN** 技能激活、执行、结束
- **THEN** 触发对应的 AbilityEventArgs 事件
- **AND** 事件包含技能信息和执行状态

#### Scenario: 效果事件
- **WHEN** 效果应用、移除、堆叠变化
- **THEN** 触发对应的 EffectEventArgs 事件
- **AND** 事件包含效果信息和变化详情

---

### Requirement: ECS Integration
系统 SHALL 与现有 ECS 架构集成，提供战斗相关的组件和系统。

#### Scenario: 战斗组件
- **WHEN** 实体需要战斗能力
- **THEN** 添加 CombatComponent 组件
- **AND** 组件包含 AttributeSet、AbilitySystem、EffectContainer

#### Scenario: 战斗系统
- **WHEN** GameWorld 更新
- **THEN** CombatSystem 更新所有战斗实体
- **AND** 处理效果tick、冷却更新、属性重算
