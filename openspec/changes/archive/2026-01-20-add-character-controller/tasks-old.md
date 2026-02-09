# Implementation Tasks

## MVP Scope (Phase 1) - 核心移动功能

### 0. 架构关键任务 [MVP - 必须先完成]
- [ ] 0.1 明确驱动关系和数据同步权威源
  - [ ] 0.1.1 确认 Bridge 是唯一驱动 Motor 的地方
  - [ ] 0.1.2 确认 CombatEntity.Tick() 不调用 Motor.Tick()
  - [ ] 0.1.3 确认 Transform 是位置的权威源
  - [ ] 0.1.4 文档化数据流向（单向）
- [ ] 0.2 定义输入抽象接口
  - [ ] 0.2.1 创建 `IInputProvider` 接口
  - [ ] 0.2.2 实现默认的 `UnityInputProvider`（用于测试）
  - [ ] 0.2.3 支持外部注入（为 AI/网络预留）
- [ ] 0.3 明确地面检测策略
  - [ ] 0.3.1 使用自定义 SphereCast，忽略 CharacterController.isGrounded
  - [ ] 0.3.2 定义检测参数（半径 0.4m，距离 0.2m）
  - [ ] 0.3.3 编写地面检测单元测试

### 1. 基础架构 [MVP]
- [ ] 1.1 创建 `Assets/Runtime/Combat/Movement/` 目录
- [ ] 1.2 定义 `MovementState` 枚举（Idle, Walking, Running, Jumping, Falling）
- [ ] 1.3 创建 `CharacterControllerConfig` 配置类（定义默认参数）
- [ ] 1.4 创建 `MovementAttributeSet` 属性集
  - [ ] 1.4.1 MoveSpeed（默认 5.0 m/s）
  - [ ] 1.4.2 JumpHeight（默认 1.5 m）
  - [ ] 1.4.3 Gravity（默认 -20.0 m/s²）
  - [ ] 1.4.4 Acceleration（默认 10.0 m/s²）
- [ ] 1.5 添加移动相关的 GameplayTag 定义
  - [ ] 1.5.1 `State.Movement.Rooted`（定身）
  - [ ] 1.5.2 `State.Movement.Slowed`（减速）
  - [ ] 1.5.3 `State.Movement.Airborne`（空中）

### 2. 核心移动逻辑（纯数据层）[MVP]
- [ ] 2.1 实现 `MovementStateMachine` 类
  - [ ] 2.1.1 定义状态转换规则和优先级（MVP 只包含 Idle/Walk/Run/Jump/Fall）
  - [ ] 2.1.2 添加可配置的 `RunThreshold` 属性（默认 3.0 m/s）
  - [ ] 2.1.3 实现 `CanTransition()` 方法
  - [ ] 2.1.4 实现 `UpdateState()` 自动状态更新
  - [ ] 2.1.5 添加状态变化事件
- [ ] 2.2 实现 `IGroundDetector` 接口（抽象层）
  - [ ] 2.2.1 定义 `IsGrounded(Vector3 position, out RaycastHit hit)` 方法
  - [ ] 2.2.2 定义 `GetGroundNormal(RaycastHit hit)` 方法
  - [ ] 2.2.3 定义 `GetSlopeAngle(Vector3 normal)` 方法
- [ ] 2.3 实现 `IInputProvider` 接口（输入抽象）
  - [ ] 2.3.1 定义 `GetMoveInput()` 方法
  - [ ] 2.3.2 定义 `GetJumpInput()` 方法
- [ ] 2.4 实现 `CharacterMotor` 类（纯数据，不依赖 Unity）
  - [ ] 2.4.1 移动输入处理（SetMoveInput, SetInputProvider）
  - [ ] 2.4.2 跳跃输入处理（Jump）
  - [ ] 2.4.3 重力计算（ApplyGravity，终端速度 -50 m/s）
  - [ ] 2.4.4 速度计算和平滑（CalculateVelocity）
  - [ ] 2.4.5 集成 MovementStateMachine
  - [ ] 2.4.6 标签影响处理（Rooted 阻止移动，Slowed 降低速度）
  - [ ] 2.4.7 提供 `CalculateMovement()` 方法（返回位移向量）
  - [ ] 2.4.8 **确保不直接修改 Transform 或 CombatEntity.Position**

### 3. Unity 集成层 [MVP]
- [ ] 3.1 实现 `GroundDetector` 类（Unity 实现）
  - [ ] 3.1.1 使用 SphereCast 检测地面（半径 0.4m，距离 0.2m）
  - [ ] 3.1.2 计算斜坡角度（基于地面法线）
  - [ ] 3.1.3 处理台阶检测（StepOffset 0.3m）
  - [ ] 3.1.4 使用配置的 GroundLayers
  - [ ] 3.1.5 **忽略 CharacterController.isGrounded，只使用自定义检测**
- [ ] 3.2 实现 `UnityInputProvider` 类（默认输入实现）
  - [ ] 3.2.1 实现 `GetMoveInput()` - 读取 Horizontal/Vertical 轴
  - [ ] 3.2.2 实现 `GetJumpInput()` - 读取 Jump 按钮
- [ ] 3.3 创建 `CharacterControllerBridge` MonoBehaviour
  - [ ] 3.3.1 持有 Unity CharacterController 组件
  - [ ] 3.3.2 持有 CharacterMotor 引用
  - [ ] 3.3.3 持有 GroundDetector 实例
  - [ ] 3.3.4 持有 IInputProvider（支持外部注入）
  - [ ] 3.3.5 在 Update 中收集输入（优先使用注入的 Provider）
  - [ ] 3.3.6 **唯一驱动点**：调用 CharacterMotor.Tick()
  - [ ] 3.3.7 应用移动到 CharacterController.Move()
  - [ ] 3.3.8 可选：同步位置到 CombatEntity.CachedPosition
  - [ ] 3.3.9 提供 SetInputProvider() 方法（用于 AI/网络）
- [ ] 3.4 配置 Unity CharacterController 参数
  - [ ] 3.4.1 Height = 2.0f, Radius = 0.5f, Center = (0, 1, 0)
  - [ ] 3.4.2 SlopeLimit = 45f, StepOffset = 0.3f, SkinWidth = 0.08f

### 4. 战斗系统集成 [MVP]
- [ ] 4.1 扩展 `CombatEntity` 类
  - [ ] 4.1.1 添加 `MovementAttributeSet` 属性（可选）
  - [ ] 4.1.2 添加 `CharacterMotor` 属性（可选）
  - [ ] 4.1.3 添加 `Vector3 CachedPosition` 属性（可选缓存）
  - [ ] 4.1.4 添加 `EnableMovement()` 工厂方法
  - [ ] 4.1.5 在 `Tick()` 中**只读同步**移动状态（IsGrounded, CurrentState）
  - [ ] 4.1.6 **确保不调用 Motor.Tick()**（避免双重驱动）
- [ ] 4.2 扩展 `CombatComponent` ECS 组件
  - [ ] 4.2.1 添加 `CharacterControllerBridge` 引用
  - [ ] 4.2.2 在组件附加时自动添加 Bridge（如果 Entity 有 Motor）
  - [ ] 4.2.3 在组件移除时清理 Bridge
  - [ ] 4.2.4 可选：同步 Transform.position 到 Entity.CachedPosition
- [ ] 4.3 实现标签对移动的影响
  - [ ] 4.3.1 Rooted 标签 → 强制 Idle 状态，忽略输入
  - [ ] 4.3.2 Slowed 标签 → 通过属性修改器降低 MoveSpeed
  - [ ] 4.3.3 Airborne 标签 → 自动添加/移除（基于 IsGrounded）
- [ ] 4.4 在 `CombatModule` 中集成
  - [ ] 4.4.1 确保 CombatEntity.Tick() 被调用（已有）
  - [ ] 4.4.2 **确认不直接驱动 CharacterMotor**（由 Bridge 负责）
  - [ ] 4.4.3 提供创建可移动实体的便捷方法

### 5. 测试和验证 [MVP]
- [ ] 5.1 编写单元测试（不需要 Unity）
  - [ ] 5.1.1 MovementStateMachine 状态转换测试
  - [ ] 5.1.2 CharacterMotor 速度计算测试
  - [ ] 5.1.3 标签影响移动测试（Rooted, Slowed）
  - [ ] 5.1.4 属性修改器对速度的影响测试
  - [ ] 5.1.5 **驱动关系测试**：确保 Motor 不直接修改外部状态
- [ ] 5.2 创建 PlayMode 测试场景
  - [ ] 5.2.1 平地移动测试（WASD 移动，空格跳跃）
  - [ ] 5.2.2 斜坡测试（45° 以下可行走，超过则滑落）
  - [ ] 5.2.3 台阶测试（0.3m 以下自动爬升）
  - [ ] 5.2.4 标签测试（应用 Rooted/Slowed 效果）
  - [ ] 5.2.5 **地面检测稳定性测试**（无抖动）
  - [ ] 5.2.6 **输入注入测试**（AI/网络模拟）
- [ ] 5.3 验收测试（基于 design.md 的验收标准）
  - [ ] 5.3.1 角色移动速度符合 MoveSpeed 属性（误差 < 5%）
  - [ ] 5.3.2 跳跃高度符合 JumpHeight 属性（误差 < 10%）
  - [ ] 5.3.3 重力正确应用，落地检测准确
  - [ ] 5.3.4 斜坡和台阶行为符合预期
  - [ ] 5.3.5 Rooted 标签阻止移动
  - [ ] 5.3.6 Slowed 效果降低速度
  - [ ] 5.3.7 状态机正确反映移动状态
  - [ ] 5.3.8 **数据流向单向**：Transform → CachedPosition，无回滚

### 6. 文档 [MVP]
- [ ] 6.1 编写 XML 注释（所有公共 API）
- [ ] 6.2 编写使用指南
  - [ ] 6.2.1 如何创建可移动的战斗实体
  - [ ] 6.2.2 如何配置移动参数
  - [ ] 6.2.3 如何通过标签/效果影响移动
- [ ] 6.3 更新 combat 模块架构文档

---

## Phase 2 - 战斗集成增强（延后）

### 7. 击退和强制位移
- [ ] 7.1 实现 `Knockback` 效果
  - [ ] 7.1.1 定义击退参数（方向、力度、持续时间）
  - [ ] 7.1.2 在 CharacterMotor 中处理强制位移
  - [ ] 7.1.3 击退期间禁用输入控制
- [ ] 7.2 实现位移技能支持
  - [ ] 7.2.1 技能可调用 `Motor.ApplyImpulse()` 方法
  - [ ] 7.2.2 支持瞬移（Blink）- 直接设置位置
  - [ ] 7.2.3 支持冲锋（Charge）- 快速移动到目标

### 8. 冲刺系统（独立能力）
- [ ] 8.1 添加 Dashing 状态到状态机
- [ ] 8.2 实现 `DashAbility` 技能
  - [ ] 8.2.1 冲刺方向和距离控制
  - [ ] 8.2.2 冲刺速度和持续时间
  - [ ] 8.2.3 冲刺冷却管理
- [ ] 8.3 冲刺碰撞处理
  - [ ] 8.3.1 碰到障碍物提前结束
  - [ ] 8.3.2 可选：冲刺期间无敌帧

---

## Phase 3 - 高级特性（延后）

### 9. Root Motion 支持
- [ ] 9.1 定义 Root Motion 接口
  - [ ] 9.1.1 从 Animator 提取位移
  - [ ] 9.1.2 混合根运动和输入控制
- [ ] 9.2 在 CharacterControllerBridge 中集成
- [ ] 9.3 处理根运动碰撞

### 10. 空中控制和二段跳
- [ ] 10.1 添加空中移动控制力度参数
- [ ] 10.2 实现二段跳能力（可选）
- [ ] 10.3 空中冲刺支持（可选）

### 11. 角色推挤
- [ ] 11.1 实现角色间碰撞检测
- [ ] 11.2 基于 Mass 属性计算推挤力
- [ ] 11.3 处理多角色重叠情况

### 12. 性能优化
- [ ] 12.1 使用 Unity Job System 批量处理移动计算
- [ ] 12.2 距离剔除（远离相机的角色降低更新频率）
- [ ] 12.3 对象池优化
- [ ] 12.4 减少射线检测次数（缓存结果）

---

## 实现优先级说明

**必须完成（MVP）：** 任务 1-6
- 这些任务构成最小可交付功能
- 完成后可以实现基础的角色移动和战斗集成
- 满足 design.md 中定义的验收标准

**后续迭代（Phase 2-3）：** 任务 7-12
- 这些是增强功能，不影响核心功能
- 可以根据实际需求逐步添加
- 每个 Phase 可以独立开发和测试
