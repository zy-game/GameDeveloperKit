# combat Specification

## Purpose
TBD - created by archiving change add-combat-system. Update Purpose after archive.
## Requirements
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

### Requirement: KCC Library Integration (MVP)
系统 SHALL 集成现有的 Kinematic Character Controller (KCC) 库，通过适配器模式桥接战斗系统和 KCC 物理引擎。

#### Scenario: 创建角色控制器适配器
- **WHEN** 在 GameObject 上添加 CombatCharacterController
- **THEN** 自动添加 KinematicCharacterMotor 组件（KCC 库）
- **AND** 由 CombatComponent 触发添加，CombatCharacterController 在 Awake 中兜底补齐缺失组件
- **AND** 配置 Motor 参数（MaxStableSlopeAngle=45°, MaxStepHeight=0.3m）
- **AND** CombatCharacterController 实现 ICharacterController 接口

#### Scenario: 移动角色
- **WHEN** KinematicCharacterMotor 调用 ICharacterController.UpdateVelocity
- **THEN** CombatCharacterController 从 CombatEntity 读取 MovementAttributes
- **AND** 计算目标速度并更新 currentVelocity 参数
- **AND** KCC Motor 应用速度到 Transform，自动处理碰撞和滑动（由 KCC 库提供）
- **AND** 移动速度误差 < 5%（实际速度与 MoveSpeed 属性的偏差）

#### Scenario: 跳跃
- **WHEN** 调用 `CombatCharacterController.Jump()` 且角色在地面上
- **THEN** 在 UpdateVelocity 回调中设置向上初速度 v = sqrt(2 * JumpHeight * |Gravity|)
- **AND** 移动状态切换为 Jumping
- **AND** 跳跃高度误差 < 10%（实际高度与 JumpHeight 属性的偏差）

#### Scenario: 查询地面状态
- **WHEN** KCC Motor 完成地面检测（PostGroundingUpdate 回调）
- **THEN** CombatCharacterController 从 Motor.GroundingStatus 读取地面状态
- **AND** 同步到 CombatEntity.IsGrounded
- **AND** 地面检测由 KCC 库提供（SphereCast + 稳定性评估）

---

### Requirement: Movement State Machine (MVP)
系统 SHALL 提供移动状态机，管理角色的移动状态转换，确保状态一致性。**MVP 阶段只包含基础状态（Idle/Walk/Run/Jump/Fall）**。

#### Scenario: 状态定义（MVP）
- **WHEN** 定义 MovementState 枚举
- **THEN** **MVP 阶段**包含以下状态：Idle(0), Walking(1), Running(2), Jumping(3), Falling(4)
- **AND** **Phase 2** 添加：Dashing(5)
- **AND** 每个状态有明确的进入和退出条件

#### Scenario: 状态转换规则
- **WHEN** 移动条件发生变化
- **THEN** CombatCharacterController 在 AfterCharacterUpdate 回调中更新状态：
  - 不在地面 + 向上速度 > 0 → Jumping
  - 不在地面 + 向上速度 ≤ 0 → Falling
  - 在地面 + 有输入 + 速度 > 3.0 m/s (RunThreshold) → Running
  - 在地面 + 有输入 + 速度 ≤ 3.0 m/s → Walking
  - 在地面 + 无输入 → Idle
- **AND** 同步到 CombatEntity.MovementState

#### Scenario: 标签强制状态
- **WHEN** 角色拥有 `State.Movement.Rooted` 标签
- **THEN** 强制进入 Idle 状态
- **AND** 忽略所有移动输入
- **AND** 标签移除后恢复正常状态转换

---

### Requirement: Ground Detection (Provided by KCC Library) (MVP)
系统 SHALL 使用 KCC 库的地面检测功能，准确判断角色与地面的关系，支持斜坡和台阶处理。

#### Scenario: 检测地面（KCC 库提供）
- **WHEN** KinematicCharacterMotor 每帧更新
- **THEN** KCC 库自动执行地面检测（SphereCast + 稳定性评估）
- **AND** 结果存储在 Motor.GroundingStatus（IsStableOnGround, GroundNormal 等）
- **AND** CombatCharacterController 在 PostGroundingUpdate 回调中读取结果

#### Scenario: 斜坡检测（KCC 库提供）
- **WHEN** 角色站在斜坡上
- **THEN** KCC 库根据 MaxStableSlopeAngle 参数判断稳定性
- **AND** 斜坡角度 ≤ 45° 时 IsStableOnGround = true
- **AND** 斜坡角度 > 45° 时 IsStableOnGround = false，角色滑落

#### Scenario: 台阶爬升（KCC 库提供）
- **WHEN** 角色前方遇到台阶
- **THEN** KCC 库根据 MaxStepHeight 参数（0.3m）自动处理
- **AND** 台阶高度 ≤ 0.3m 时自动爬升
- **AND** 台阶高度 > 0.3m 时视为障碍物，阻止移动

---

### Requirement: Movement Attributes
系统 SHALL 提供移动相关的属性集，控制角色的移动参数，所有属性支持通过修改器动态调整。

#### Scenario: 定义移动属性
- **WHEN** 创建 MovementAttributeSet
- **THEN** 包含以下属性及默认值：
  - MoveSpeed = 5.0 m/s（移动速度）
  - JumpHeight = 1.5 m（跳跃高度）
  - Gravity = -20.0 m/s²（重力加速度）
  - Acceleration = 10.0 m/s²（加速度）
- **AND** 每个属性支持 Add、Multiply、Override 三种修改器

#### Scenario: 修改移动速度
- **WHEN** 应用移动速度修改器（如 Multiply 0.5，即减速 50%）
- **THEN** 角色的实际移动速度 = BaseValue * 0.5
- **AND** 修改器移除后恢复到 BaseValue
- **AND** 多个修改器按优先级叠加（Add → Multiply → Override）

#### Scenario: 重力影响
- **WHEN** 角色不在地面上（IsGrounded = false）
- **THEN** 每帧应用重力加速度：velocity.y += Gravity * deltaTime
- **AND** 重力值可通过属性修改器调整（如低重力区域）
- **AND** 终端速度限制为 -50 m/s（防止无限加速）

---

### Requirement: Movement Tags (MVP)
系统 SHALL 提供移动相关的 GameplayTag，用于控制移动状态和限制，标签变化立即生效。

#### Scenario: 定身标签
- **WHEN** 角色拥有 `State.Movement.Rooted` 标签
- **THEN** 角色无法移动（速度强制为 0）
- **AND** 移动输入被忽略
- **AND** 状态强制为 Idle

#### Scenario: 减速标签
- **WHEN** 角色拥有 `State.Movement.Slowed` 标签
- **THEN** 通过属性修改器降低 MoveSpeed（如 Multiply 0.5）
- **AND** 减速效果可堆叠（多个 Slowed 效果叠加）
- **AND** 最终速度 = BaseSpeed * modifier1 * modifier2 * ...

#### Scenario: 空中标签
- **WHEN** 角色不在地面上（IsGrounded = false）
- **THEN** 自动添加 `State.Movement.Airborne` 标签
- **AND** 落地后（IsGrounded = true）自动移除
- **AND** 技能可查询此标签判断是否在空中

---

### Requirement: Combat Entity Integration (MVP)
系统 SHALL 将角色控制器集成到 CombatEntity，实现战斗实体的移动能力，保持数据层和表现层分离。

#### Scenario: 创建可移动战斗实体
- **WHEN** 调用 `CombatEntity.EnableMovement()`
- **THEN** 创建 MovementAttributeSet（使用默认值）
- **AND** 实体具备移动能力
- **AND** CombatComponent 自动添加 CombatCharacterController 和 KinematicCharacterMotor

#### Scenario: 只读同步移动状态
- **WHEN** CombatEntity.Tick(deltaTime) 调用
- **THEN** 从 CombatCharacterController 只读同步状态（IsGrounded, MovementState）
- **AND** 从 Transform 只读同步位置到 CachedPosition（可选）
- **AND** 可用于战斗逻辑判断（如空中技能限制）
- **AND** **不调用 Motor.Tick()**（由 KinematicCharacterMotor 自动更新）

#### Scenario: 技能影响移动
- **WHEN** 技能激活并授予 `State.Movement.Rooted` 标签
- **THEN** CombatCharacterController 在 UpdateVelocity 回调中检测到标签
- **AND** 强制速度为 0，阻止移动
- **AND** 技能结束移除标签后，恢复移动能力

#### Scenario: 死亡停止移动
- **WHEN** CombatEntity.IsDead = true
- **THEN** CombatCharacterController 检测到死亡状态
- **AND** 停止响应输入
- **AND** 速度逐渐减为 0（带阻尼）

---

