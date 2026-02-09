## ADDED Requirements

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

## Future Requirements (Phase 2-3, Not in MVP)

以下需求将在后续阶段实现，不包含在本次 MVP 范围内。

**注：碰撞处理（滑动、穿透修正）已由 KCC 库提供，无需额外实现。Phase 2-3 只需实现高级特性。**

---

### Requirement: Dash System (Phase 2)
系统 SHALL 提供冲刺系统，支持快速位移能力。

#### Scenario: 执行冲刺
- **WHEN** 调用 `Dash(Vector3 direction, float distance)`
- **THEN** 角色快速向指定方向移动
- **AND** 移动状态切换为 Dashing

#### Scenario: 冲刺冷却
- **WHEN** 冲刺执行完毕
- **THEN** 进入冷却状态
- **AND** 冷却期间无法再次冲刺

#### Scenario: 冲刺中断
- **WHEN** 冲刺期间碰到障碍物
- **THEN** 冲刺提前结束
- **AND** 角色停在障碍物前

---

### Requirement: Root Motion Support (Phase 3)
系统 SHALL 支持根运动（Root Motion），允许动画驱动角色移动。

#### Scenario: 启用根运动
- **WHEN** 设置 `UseRootMotion = true`
- **THEN** 从 Animator 提取位移和旋转
- **AND** 应用到角色控制器

#### Scenario: 混合根运动和输入
- **WHEN** 同时存在根运动和输入控制
- **THEN** 根据混合权重计算最终移动
- **AND** 支持平滑过渡

#### Scenario: 根运动碰撞
- **WHEN** 根运动导致角色碰撞
- **THEN** 应用碰撞检测和响应
- **AND** 防止穿透

---

### Requirement: Movement Effects Integration (Phase 2)
系统 SHALL 支持效果系统对移动的影响，实现战斗技能与移动的集成。

**注意：** MVP 阶段只支持定身和减速效果（通过标签和属性修改器），击退和位移技能在 Phase 2 实现。

#### Scenario: 定身效果 (MVP)
- **WHEN** 应用定身效果（授予 Rooted 标签）
- **THEN** 角色无法移动
- **AND** 效果结束后恢复移动能力

#### Scenario: 减速效果 (MVP)
- **WHEN** 应用减速效果（修改 MoveSpeed 属性）
- **THEN** 角色移动速度降低
- **AND** 效果可堆叠（多个减速效果叠加）

#### Scenario: 击退效果 (Phase 2)
- **WHEN** 应用击退效果
- **THEN** 角色被强制向指定方向移动
- **AND** 击退期间无法控制移动方向

#### Scenario: 位移技能 (Phase 2)
- **WHEN** 技能执行位移（如闪现、冲锋）
- **THEN** 通过角色控制器执行位移
- **AND** 检测路径上的碰撞

---
