# Implementation Tasks: 集成 KCC 到战斗系统

## MVP Scope (Phase 1) - 核心集成功能

### 0. 架构关键任务 [MVP - 必须先完成]
- [x] 0.1 理解 KCC 库接口
  - [x] 0.1.1 阅读 `ICharacterController` 接口文档
  - [x] 0.1.2 研究 `ExampleCharacterController` 示例实现
  - [x] 0.1.3 理解 `KinematicCharacterMotor` 的回调机制
  - [x] 0.1.4 理解 `CharacterGroundingReport` 和 `HitStabilityReport` 结构
- [x] 0.2 明确驱动关系和数据同步权威源
  - [x] 0.2.1 确认 KinematicCharacterMotor 是唯一驱动者
  - [x] 0.2.2 确认 CombatEntity 只读同步状态
  - [x] 0.2.3 确认 Transform 是位置的权威源
  - [x] 0.2.4 文档化数据流向（单向）
- [x] 0.3 定义输入抽象接口
  - [x] 0.3.1 创建 `IInputProvider` 接口
  - [x] 0.3.2 实现默认的 `UnityInputProvider`（用于测试）
  - [x] 0.3.3 支持外部注入（为 AI/网络预留）

### 1. 基础架构 [MVP]
- [x] 1.1 创建 `Assets/Runtime/Combat/Movement/` 目录
- [x] 1.2 定义 `MovementState` 枚举（Idle, Walking, Running, Jumping, Falling）
- [x] 1.3 创建 `MovementAttributeSet` 类
  - [x] 1.3.1 定义 MoveSpeed 属性（默认 5.0 m/s）
  - [x] 1.3.2 定义 JumpHeight 属性（默认 1.5 m）
  - [x] 1.3.3 定义 Gravity 属性（默认 -20.0 m/s²）
  - [x] 1.3.4 定义 Acceleration 属性（默认 10.0 m/s²）
  - [x] 1.3.5 定义 Mass 属性（默认 70.0 kg，Phase 3 才使用）
- [x] 1.4 定义移动相关 GameplayTag
  - [x] 1.4.1 `State.Movement.Rooted`（定身）
  - [x] 1.4.2 `State.Movement.Slowed`（减速）
  - [x] 1.4.3 `State.Movement.Airborne`（空中，自动管理）

### 2. 适配器实现（ICharacterController）[MVP]
- [x] 2.1 创建 `CombatCharacterController` 类
  - [x] 2.1.1 继承 `MonoBehaviour`，实现 `ICharacterController` 接口
  - [x] 2.1.2 持有 `KinematicCharacterMotor` 引用
  - [x] 2.1.3 持有 `CombatEntity` 引用
  - [x] 2.1.4 持有 `IInputProvider` 引用（可外部注入）
- [x] 2.2 实现 `ICharacterController.UpdateVelocity`
  - [x] 2.2.1 从 CombatEntity 读取 MovementAttributes
  - [x] 2.2.2 检查 Rooted 标签（强制速度为 0）
  - [x] 2.2.3 检查 Slowed 标签（降低速度）
  - [x] 2.2.4 地面移动逻辑（基于 Motor.GroundingStatus）
  - [x] 2.2.5 空中移动逻辑（应用重力）
  - [x] 2.2.6 跳跃逻辑（计算初速度）
- [x] 2.3 实现 `ICharacterController.UpdateRotation`
  - [x] 2.3.1 根据移动输入计算目标旋转
  - [x] 2.3.2 平滑插值到目标旋转
- [x] 2.4 实现 `ICharacterController.PostGroundingUpdate`
  - [x] 2.4.1 同步 IsGrounded 到 CombatEntity
  - [x] 2.4.2 自动管理 Airborne 标签
- [x] 2.5 实现 `ICharacterController.AfterCharacterUpdate`
  - [x] 2.5.1 同步 Transform.position 到 CombatEntity.CachedPosition
  - [x] 2.5.2 更新 CombatEntity.MovementState
- [x] 2.6 实现其他接口方法（简化实现）
  - [x] 2.6.1 `BeforeCharacterUpdate`（空实现）
  - [x] 2.6.2 `IsColliderValidForCollisions`（返回 true）
  - [x] 2.6.3 碰撞回调方法（空实现或日志）
- [x] 2.7 实现输入接口
  - [x] 2.7.1 `SetMoveInput(Vector3 input)` 方法
  - [x] 2.7.2 `Jump()` 方法
  - [x] 2.7.3 `SetInputProvider(IInputProvider provider)` 方法
  - [x] 2.7.4 在 Update 中收集输入（优先使用注入的 Provider）

### 3. 输入抽象实现 [MVP]
- [x] 3.1 创建 `IInputProvider` 接口
  - [x] 3.1.1 定义 `GetMoveInput()` 方法
  - [x] 3.1.2 定义 `GetJumpInput()` 方法
- [x] 3.2 实现 `UnityInputProvider` 类（默认实现）
  - [x] 3.2.1 实现 `GetMoveInput()` - 读取 Horizontal/Vertical 轴
  - [x] 3.2.2 实现 `GetJumpInput()` - 读取 Jump 按钮

### 4. 战斗系统集成 [MVP]
- [x] 4.1 扩展 `CombatEntity` 类
  - [x] 4.1.1 添加 `MovementAttributeSet` 属性（可选）
  - [x] 4.1.2 添加 `MovementState` 属性
  - [x] 4.1.3 添加 `IsGrounded` 属性
  - [x] 4.1.4 添加 `Vector3 CachedPosition` 属性（可选缓存）
  - [x] 4.1.5 添加 `EnableMovement()` 工厂方法
  - [x] 4.1.6 在 `Tick()` 中**只读同步**移动状态（不驱动 Motor）
- [x] 4.2 扩展 `CombatComponent` ECS 组件
  - [x] 4.2.1 添加 `CombatCharacterController` 引用
  - [x] 4.2.2 在组件附加时自动添加 CombatCharacterController 和 KinematicCharacterMotor
  - [x] 4.2.3 配置 KinematicCharacterMotor 参数（MaxStableSlopeAngle=45°, MaxStepHeight=0.3m）
  - [x] 4.2.4 在组件移除时清理组件
  - [x] 4.2.5 可选：同步 Transform.position 到 Entity.CachedPosition
- [x] 4.3 实现标签对移动的影响
  - [x] 4.3.1 Rooted 标签 → 强制 Idle 状态，速度为 0
  - [x] 4.3.2 Slowed 标签 → 通过属性修改器降低 MoveSpeed
  - [x] 4.3.3 Airborne 标签 → 自动添加/移除（基于 IsGrounded）
- [x] 4.4 在 `CombatModule` 中集成
  - [x] 4.4.1 确保 CombatEntity.Tick() 被调用（已有）
  - [x] 4.4.2 **确认不直接驱动 Motor**（由 KCC 负责）
  - [x] 4.4.3 提供创建可移动实体的便捷方法

### 5. 测试和验证 [MVP]
- [ ] 5.1 编写单元测试（不需要 Unity）
  - [ ] 5.1.1 MovementAttributeSet 属性计算测试
  - [ ] 5.1.2 标签影响移动测试（Rooted, Slowed）
  - [ ] 5.1.3 属性修改器对速度的影响测试
  - [ ] 5.1.4 状态转换逻辑测试
- [ ] 5.2 创建 PlayMode 测试场景
  - [ ] 5.2.1 平地移动测试（WASD 移动，空格跳跃）
  - [ ] 5.2.2 斜坡测试（使用 KCC 库的斜坡处理）
  - [ ] 5.2.3 台阶测试（使用 KCC 库的台阶爬升）
  - [ ] 5.2.4 标签测试（应用 Rooted/Slowed 效果）
  - [ ] 5.2.5 **地面检测稳定性测试**（KCC 库提供）
  - [ ] 5.2.6 **输入注入测试**（AI/网络模拟）
- [ ] 5.3 验收测试（基于 design.md 的验收标准）
  - [ ] 5.3.1 角色移动速度符合 MoveSpeed 属性（误差 < 5%）
  - [ ] 5.3.2 跳跃高度符合 JumpHeight 属性（误差 < 10%）
  - [ ] 5.3.3 重力正确应用，落地检测准确
  - [ ] 5.3.4 斜坡和台阶行为符合 KCC 库预期
  - [ ] 5.3.5 Rooted 标签阻止移动
  - [ ] 5.3.6 Slowed 效果降低速度
  - [ ] 5.3.7 状态机正确反映移动状态
  - [ ] 5.3.8 **数据流向单向**：Transform → CachedPosition，无回滚
- [ ] 5.4 性能测试
  - [ ] 5.4.1 测试 100+ 角色同时移动的性能
  - [ ] 5.4.2 使用 Profiler 监控 CPU 和内存使用
  - [ ] 5.4.3 确认 KCC 库的性能符合预期

### 6. 文档 [MVP]
- [ ] 6.1 编写 XML 注释（所有公共 API）
- [x] 6.2 编写使用示例
  - [x] 6.2.1 如何创建可移动的 CombatEntity
  - [x] 6.2.2 如何应用标签影响移动
  - [x] 6.2.3 如何注入自定义输入源（AI/网络）
- [x] 6.3 更新战斗系统文档
  - [x] 6.3.1 添加移动系统章节
  - [x] 6.3.2 说明 KCC 库集成方式
  - [x] 6.3.3 说明数据流向和驱动关系
- [ ] 6.4 创建配置指南
  - [ ] 6.4.1 KinematicCharacterMotor 参数说明
  - [ ] 6.4.2 MovementAttributeSet 默认值说明
  - [ ] 6.4.3 常见问题和解决方案

---

## Phase 2 - 高级特性（延后）

### 7. 击退和位移效果 [Phase 2]
- [x] 7.1 实现击退效果
  - [x] 7.1.1 使用 `Motor.ForceUnground()` 强制离地
  - [x] 7.1.2 叠加击退速度到 Motor.Velocity
  - [x] 7.1.3 击退期间禁用输入控制
- [x] 7.2 实现位移技能
  - [x] 7.2.1 闪现技能（瞬移）
  - [x] 7.2.2 冲锋技能（快速移动）
  - [x] 7.2.3 路径碰撞检测

### 8. 冲刺系统 [Phase 2]
- [x] 8.1 添加 Dashing 状态到 MovementState 枚举
- [x] 8.2 实现冲刺逻辑
  - [x] 8.2.1 冲刺速度计算
  - [x] 8.2.2 冲刺持续时间和冷却
  - [x] 8.2.3 冲刺中断条件
- [x] 8.3 集成到 CombatCharacterController

### 9. 碰撞推挤 [Phase 3]
- [x] 9.1 配置 KinematicCharacterMotor 的刚体交互
  - [x] 9.1.1 设置 `RigidbodyInteractionType = SimulatedDynamic`
  - [x] 9.1.2 使用 `SimulatedCharacterMass` 属性
- [x] 9.2 实现角色间推挤
  - [x] 9.2.1 使用 Mass 属性计算推挤力
  - [x] 9.2.2 处理推挤碰撞回调

### 10. Root Motion 支持 [Phase 3]
- [x] 10.1 研究 KCC 库的 Root Motion 示例
- [x] 10.2 集成 Animator 组件
- [x] 10.3 在 UpdateVelocity 中混合 Root Motion 和输入控制

---

## 关键差异说明（相比原提案）

### 不再需要实现的功能（由 KCC 库提供）
- ❌ ~~CharacterMotor 类~~（使用 KinematicCharacterMotor）
- ❌ ~~CharacterControllerBridge 类~~（使用 CombatCharacterController 适配器）
- ❌ ~~GroundDetector 类~~（KCC 库已实现）
- ❌ ~~自定义地面检测逻辑~~（使用 Motor.GroundingStatus）
- ❌ ~~碰撞处理逻辑~~（KCC 库已实现）
- ❌ ~~斜坡和台阶处理~~（KCC 库已实现）

### 新增的集成任务
- ✅ 实现 `ICharacterController` 接口（适配器模式）
- ✅ 理解 KCC 库的回调机制
- ✅ 配置 KinematicCharacterMotor 参数
- ✅ 复用 KCC 库的所有物理功能

### 工作量估算
- **原提案**：~105 个子任务（73 MVP + 32 Phase 2-3）
- **修订提案**：~70 个子任务（50 MVP + 20 Phase 2-3）
- **减少原因**：复用 KCC 库的成熟实现，避免重复造轮子
