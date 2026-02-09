# Change: 集成 Kinematic Character Controller 到战斗系统

## Why

当前战斗系统已完成技能、属性、效果、伤害等核心战斗逻辑，但缺少角色移动控制能力。为了实现完整的战斗体验，需要集成角色移动控制器来处理角色的移动、跳跃、碰撞检测等物理交互。

**项目现状：**
- ✅ 项目已有完整的 **Kinematic Character Controller (KCC)** 库（第三方资产）
- ✅ KCC 库提供成熟的物理引擎：地面检测、斜坡/台阶处理、碰撞响应、移动平台支持
- ❌ KCC 库未与战斗系统集成

**本提案目标：**
- **不重新实现 KCC**，而是通过适配器模式集成现有库
- 实现 `ICharacterController` 接口桥接战斗系统和 KCC 库
- 复用 KCC 库的所有物理功能
- 保持战斗系统的纯数据架构

集成后将实现：
- 流畅的角色移动控制（行走、奔跑、跳跃）
- 与技能系统集成（技能可影响移动状态）
- 标签和属性对移动的影响（定身、减速等）
- 完整的物理交互（由 KCC 库提供）

## What Changes

### 新增组件和类型

1. **CombatCharacterController 类**（适配器，实现 ICharacterController）
   - 实现 KCC 库的 `ICharacterController` 接口
   - 桥接 `CombatEntity` 和 `KinematicCharacterMotor`
   - 从 CombatEntity 读取属性和标签，计算速度和旋转
   - 同步状态回 CombatEntity（IsGrounded, MovementState）

2. **MovementState 枚举**（MVP 阶段）
   - `Idle`, `Walking`, `Running`, `Jumping`, `Falling`
   - 注：`Dashing` 状态在 Phase 2 添加

3. **MovementAttributeSet**（独立属性集）
   - MoveSpeed, JumpHeight, Gravity, Acceleration
   - Mass（Phase 3 才使用，用于推挤）

4. **移动相关 GameplayTag**
   - `State.Movement.Rooted`（定身，MVP）
   - `State.Movement.Slowed`（减速，MVP）
   - `State.Movement.Airborne`（空中，自动管理，MVP）

5. **输入抽象接口**
   - `IInputProvider` 接口（支持玩家/AI/网络输入）
   - `UnityInputProvider` 默认实现（用于测试）

### 扩展现有组件

- **CombatEntity**：添加 `MovementAttributes`, `IsGrounded`, `MovementState`, `CachedPosition` 属性
- **CombatComponent**：自动管理 `CombatCharacterController` 和 `KinematicCharacterMotor` 生命周期

### 复用 KCC 库功能（无需实现）

- ✅ 地面检测（SphereCast + 稳定性评估）
- ✅ 斜坡处理（可配置最大角度）
- ✅ 台阶爬升（可配置最大高度）
- ✅ 碰撞响应和滑动
- ✅ 移动平台支持
- ✅ 边缘检测和处理
- ✅ 刚体交互

### MVP 范围（Phase 1）

- 实现 `CombatCharacterController` 适配器
- 实现 `ICharacterController` 接口的关键方法
- 基础移动（WASD）、跳跃、重力
- 状态机（Idle/Walk/Run/Jump/Fall）
- 标签影响（Rooted 阻止移动，Slowed 降低速度）
- 输入抽象（支持 AI/网络）

### 延后特性（Phase 2-3）

- 击退效果、位移技能（Phase 2）
- 冲刺系统（Dashing 状态，Phase 2）
- Root Motion 支持（Phase 3）
- 碰撞推挤（Phase 3）

## Impact
- **Affected specs**: `combat`
- **Affected code**: 
  - `Assets/Runtime/Combat/Core/CombatEntity.cs` - 添加移动相关属性
  - `Assets/Runtime/Combat/Attributes/CommonAttributeSets.cs` - 添加 MovementAttributeSet
  - 新增 `Assets/Runtime/Combat/Movement/CombatCharacterController.cs` - 适配器实现
  - 新增 `Assets/Runtime/Combat/Movement/IInputProvider.cs` - 输入抽象
  - 新增 `Assets/Runtime/Combat/Movement/MovementState.cs` - 状态枚举
- **Breaking changes**: 无
- **Dependencies**: 
  - **Kinematic Character Controller** 库（已存在于 `Assets/KinematicCharacterController/`）
  - 注：不依赖 Unity 内置的 CharacterController，使用 KCC 库的 KinematicCharacterMotor
- **Integration approach**: 适配器模式，实现 `ICharacterController` 接口
