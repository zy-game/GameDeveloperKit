# Design Document: Character Controller (KCC)

## Context

当前战斗系统基于纯数据类 `CombatEntity`，不依赖 Unity MonoBehaviour。但角色控制器需要 Unity 的 `CharacterController` 组件（必须挂载在 GameObject 上），存在架构冲突。

**现有架构：**
- `CombatEntity` - 纯 C# 数据类，管理属性、技能、效果
- `CombatModule` - 实现 `IModule`，在 `Game.Combat` 中更新所有实体
- `CombatComponent` - ECS 组件，桥接 `CombatEntity` 和 Unity GameObject

**约束：**
- Unity `CharacterController` 必须挂载在 GameObject 上
- 需要保持 `CombatEntity` 的纯数据特性（便于测试和序列化）
- 需要与现有 ECS 架构兼容

## Goals / Non-Goals

### Goals
- 提供流畅的角色移动控制（移动、跳跃、重力）
- 与战斗系统深度集成（标签影响移动、技能触发位移）
- 支持基础物理交互（地面检测、斜坡、台阶）
- 保持架构清晰，数据与表现分离

### Non-Goals（延后到后续迭代）
- Root Motion 支持（需要动画系统集成）
- 复杂的空中控制（二段跳、空中冲刺）
- 网络同步（留给网络模块处理）
- 高级碰撞推挤（角色间推挤）

## Decisions

### 1. 架构设计：桥接模式

**决策：** 使用桥接模式分离数据层和表现层

```
[CombatEntity] (纯数据)
      ↓ 持有引用
[CharacterMotor] (纯数据，移动逻辑)
      ↓ 被驱动
[CharacterControllerBridge] (MonoBehaviour，Unity 集成)
      ↓ 持有
[Unity CharacterController] (Unity 组件)
```

**理由：**
- 保持 `CombatEntity` 的纯数据特性
- `CharacterMotor` 包含移动逻辑但不依赖 Unity
- `CharacterControllerBridge` 负责与 Unity 组件交互
- 便于单元测试（可以测试 `CharacterMotor` 而不需要 Unity）

**替代方案：**
- ❌ 直接在 `CombatEntity` 中持有 `CharacterController` - 破坏纯数据特性
- ❌ 完全重写物理系统 - 工作量大且不必要

### 2. 组件归属和生命周期

**决策：** 扩展现有的 `CombatComponent`（ECS 组件）

```csharp
public class CombatComponent : IComponent
{
    public CombatEntity Entity { get; set; }
    public CharacterControllerBridge ControllerBridge { get; set; } // 新增
    
    // 在 GameObject 上自动添加/移除 CharacterControllerBridge
}
```

**生命周期和驱动关系（单一权威源）：**

```
[输入源] → [Bridge.Update] → [Motor.Tick] → [Bridge 应用移动] → [Transform]
                                    ↓
                            [CombatEntity 只读同步]
```

**明确的职责划分：**
1. **CharacterControllerBridge（驱动者）**：
   - 在 `Update()` 中收集输入（或从外部注入）
   - 调用 `CharacterMotor.Tick(deltaTime, groundDetector)`
   - 获取 `Motor.CalculateMovement()` 并应用到 `CharacterController.Move()`
   - **权威源**：Bridge 是唯一驱动 Motor 的地方

2. **CharacterMotor（逻辑层）**：
   - 处理移动逻辑、状态机、标签影响
   - **不直接修改** Transform 或 CombatEntity 位置
   - 只计算并返回移动向量

3. **CombatEntity（数据层）**：
   - **只读同步**：在 `Tick()` 中从 Motor 读取状态（IsGrounded, CurrentState, Velocity）
   - **不驱动** Motor（避免双重更新）
   - 位置由 Transform 权威，CombatEntity 可选地缓存 Transform.position

4. **CombatModule**：
   - 调用 `CombatEntity.Tick()` 更新战斗逻辑（技能、效果、属性）
   - **不直接驱动** CharacterMotor（由 Bridge 负责）

**数据流向（单向）：**
```
Transform.position (权威) → CombatEntity.Position (缓存，可选)
Motor.IsGrounded → CombatEntity (只读)
Motor.CurrentState → CombatEntity (只读)
CombatEntity.Tags → Motor (读取，影响移动)
```

**避免的反模式：**
- ❌ CombatModule 和 Bridge 同时驱动 Motor（双重更新）
- ❌ Motor 直接修改 CombatEntity.Position（破坏单向流）
- ❌ CombatEntity.Tick() 调用 Motor.Tick()（与 Bridge 冲突）

### 3. 属性集设计

**决策：** 创建独立的 `MovementAttributeSet`，不与 `CombatAttributeSet` 合并

```csharp
public class MovementAttributeSet : AttributeSet
{
    // 基础移动
    public AttributeValue MoveSpeed { get; private set; }      // 默认: 5.0 m/s
    public AttributeValue Acceleration { get; private set; }   // 默认: 10.0 m/s²
    
    // 跳跃和重力
    public AttributeValue JumpHeight { get; private set; }     // 默认: 1.5 m
    public AttributeValue Gravity { get; private set; }        // 默认: -20.0 m/s²
    
    // 物理参数（Phase 3 才使用）
    public AttributeValue Mass { get; private set; }           // 默认: 70.0 kg（用于推挤，MVP 不使用）
}
```

**理由：**
- 职责分离：战斗属性和移动属性是不同的关注点
- 可选性：不是所有战斗实体都需要移动能力（如炮塔、陷阱）
- 扩展性：未来可以添加更多移动相关属性而不影响战斗属性

**CombatEntity 扩展：**
```csharp
public class CombatEntity
{
    // 现有属性集
    public HealthAttributeSet HealthAttributes { get; }
    public CombatAttributeSet CombatAttributes { get; }
    public ResourceAttributeSet ResourceAttributes { get; }
    
    // 新增（可选）
    public MovementAttributeSet MovementAttributes { get; private set; }
    public CharacterMotor Motor { get; private set; }
    
    // 工厂方法
    public void EnableMovement(MovementAttributeSet attributes = null)
    {
        MovementAttributes = attributes ?? new MovementAttributeSet();
        Motor = new CharacterMotor(this);
    }
}
```

### 4. CharacterController 默认参数

**决策：** 定义标准的默认参数，支持运行时调整

```csharp
public class CharacterControllerConfig
{
    // 碰撞体参数
    public float Height { get; set; } = 2.0f;           // 角色高度
    public float Radius { get; set; } = 0.5f;           // 碰撞半径
    public Vector3 Center { get; set; } = new(0, 1, 0); // 中心偏移
    
    // 物理参数
    public float SlopeLimit { get; set; } = 45f;        // 最大可行走坡度
    public float StepOffset { get; set; } = 0.3f;       // 最大台阶高度
    public float SkinWidth { get; set; } = 0.08f;       // 皮肤宽度（防穿透）
    
    // 地面检测
    public float GroundCheckDistance { get; set; } = 0.2f;  // 地面检测距离
    public float GroundCheckRadius { get; set; } = 0.4f;    // 地面检测半径
    public LayerMask GroundLayers { get; set; } = ~0;       // 地面层级
}
```

**来源：**
- 基于 Unity 标准人形角色的常用值
- 可通过配置文件或 ScriptableObject 覆盖
- 支持运行时动态调整（如角色变大/变小）

### 5. 移动状态机

**决策：** 使用优先级状态机，明确转换规则

```csharp
public enum MovementState
{
    Idle = 0,      // 优先级最低
    Walking = 1,
    Running = 2,
    Jumping = 3,
    Falling = 4
    // Dashing = 5  // Phase 2 才添加，MVP 不包含
}

// 状态转换规则
public class MovementStateMachine
{
    // 转换条件
    private bool CanTransition(MovementState from, MovementState to)
    {
        // Dashing 状态不能被打断（除非完成或碰撞）
        if (currentState == MovementState.Dashing && to != MovementState.Idle)
            return false;
            
        // 空中状态只能转换为 Falling 或 Idle（落地）
        if (currentState == MovementState.Jumping || currentState == MovementState.Falling)
            return to == MovementState.Falling || to == MovementState.Idle;
            
        return true;
    }
    
    // 自动状态更新
    public void UpdateState(bool isGrounded, Vector3 velocity, bool hasInput)
    {
        if (!isGrounded)
        {
            SetState(velocity.y > 0 ? MovementState.Jumping : MovementState.Falling);
        }
        else if (hasInput)
        {
            float speed = velocity.magnitude;
            SetState(speed > runThreshold ? MovementState.Running : MovementState.Walking);
        }
        else
        {
            SetState(MovementState.Idle);
        }
    }
}
```

**冲突处理：**
- 标签优先：如果有 `State.Movement.Rooted` 标签，强制进入 Idle 状态
- 效果优先：击退等强制位移效果会覆盖输入控制

### 6. MVP 范围定义

**Phase 1: 核心移动（MVP）**
- ✅ 基础移动（WASD 输入 → 移动）
- ✅ 跳跃（空格键 → 跳跃）
- ✅ 重力和地面检测
- ✅ 状态机（Idle/Walk/Run/Jump/Fall）
- ✅ 移动属性（MoveSpeed, JumpHeight, Gravity）
- ✅ 标签影响（Rooted 阻止移动，Slowed 降低速度）
- ✅ 斜坡和台阶处理

**Phase 2: 战斗集成（后续）**
- ⏸️ 击退效果（Knockback）
- ⏸️ 位移技能（Dash/Blink）
- ⏸️ 冲刺系统（独立的 Dash 能力）

**Phase 3: 高级特性（延后）**
- ⏸️ Root Motion 支持
- ⏸️ 空中控制（二段跳）
- ⏸️ 角色推挤

**验收标准（MVP）：**
1. 角色可以通过输入移动，速度符合 MoveSpeed 属性
2. 角色可以跳跃，跳跃高度符合 JumpHeight 属性
3. 角色受重力影响，落地后正确检测地面
4. 角色可以在 45° 以下的斜坡上行走
5. 角色可以自动爬升 0.3m 以下的台阶
6. 应用 Rooted 标签后，角色无法移动
7. 应用 Slowed 效果后，角色速度降低对应百分比
8. 状态机正确反映当前移动状态

## Risks / Trade-offs

### Risk 1: Unity 组件依赖
**风险：** `CharacterController` 必须在 Unity 环境中运行，增加测试复杂度

**缓解：**
- `CharacterMotor` 是纯数据类，可以独立测试逻辑
- 使用接口抽象 Unity 依赖，便于 Mock
- 提供 PlayMode 测试场景

### Risk 2: 性能开销
**风险：** 每帧更新所有角色控制器可能影响性能

**缓解：**
- 使用 Unity Job System 批量处理移动计算（后续优化）
- 距离剔除：远离相机的角色降低更新频率
- 对象池：复用 `CharacterMotor` 实例

### Risk 3: 与动画系统的集成
**风险：** 移动状态需要驱动动画，但动画系统尚未定义

**缓解：**
- 提供 `OnMoveStateChanged` 事件，由外部监听并驱动动画
- 预留 Root Motion 接口，但不在 MVP 中实现
- 文档说明如何集成 Animator

### Risk 4: 网络同步
**风险：** 移动状态需要网络同步，但同步策略未定义

**缓解：**
- `CharacterMotor` 提供序列化接口（位置、速度、状态）
- 留给网络模块处理同步逻辑
- 支持客户端预测和服务器校正（接口预留）

## Migration Plan

### Phase 1: 添加新功能（无破坏性）
1. 添加 `MovementAttributeSet` 类
2. 添加 `CharacterMotor` 类（纯数据）
3. 添加 `CharacterControllerBridge` MonoBehaviour
4. 扩展 `CombatEntity.EnableMovement()` 方法
5. 扩展 `CombatComponent` 支持 Bridge

### Phase 2: 集成到现有系统
1. 在 `CombatModule` 中更新 `CharacterMotor`
2. 添加移动相关的 GameplayTag
3. 实现标签对移动的影响
4. 添加示例场景和测试

### Rollback Plan
- 如果出现严重问题，可以禁用 `EnableMovement()` 调用
- 现有战斗系统不受影响（移动是可选功能）
- 可以逐步回滚到纯战斗系统

## Open Questions

1. **Q: 是否需要支持多种移动模式（如飞行、游泳）？**
   - A: MVP 只支持地面移动，飞行/游泳延后到后续迭代

2. **Q: 如何处理角色大小变化（如变身技能）？**
   - A: 提供 `CharacterMotor.SetSize(height, radius)` 方法，动态调整 CharacterController

3. **Q: 是否需要支持客户端预测？**
   - A: 预留接口，但实现留给网络模块

4. **Q: 如何处理多个减速效果叠加？**
   - A: 使用属性修改器的乘法堆叠（如 0.7 * 0.8 = 0.56，即 44% 减速）

## Implementation Notes

### 关键代码结构

```csharp
// 纯数据层
public class CharacterMotor
{
    private CombatEntity _entity;
    private MovementStateMachine _stateMachine;
    private IInputProvider _inputProvider; // 输入抽象
    
    public Vector3 Velocity { get; private set; }
    public bool IsGrounded { get; private set; }
    public MovementState CurrentState => _stateMachine.CurrentState;
    
    // 输入接口（支持外部注入）
    public void SetInputProvider(IInputProvider provider);
    public void SetMoveInput(Vector3 direction); // 直接设置（用于 AI/网络）
    public void Jump();
    
    // 物理更新（不依赖 Unity）
    public void Tick(float deltaTime, IGroundDetector groundDetector);
    
    // 应用到 Unity（由 Bridge 调用）
    public Vector3 CalculateMovement(float deltaTime);
}

// 输入抽象（支持玩家/AI/网络）
public interface IInputProvider
{
    Vector3 GetMoveInput();
    bool GetJumpInput();
}

// Unity 集成层
public class CharacterControllerBridge : MonoBehaviour
{
    private CharacterController _controller;
    private CharacterMotor _motor;
    private GroundDetector _groundDetector;
    private IInputProvider _inputProvider; // 可外部注入
    
    private void Update()
    {
        // 收集输入（优先使用注入的 Provider）
        if (_inputProvider != null)
        {
            _motor.SetMoveInput(_inputProvider.GetMoveInput());
            if (_inputProvider.GetJumpInput())
                _motor.Jump();
        }
        else
        {
            // 默认使用 Unity Input（仅用于原型测试）
            Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            _motor.SetMoveInput(input);
            
            if (Input.GetButtonDown("Jump"))
                _motor.Jump();
        }
        
        // 更新逻辑（唯一驱动点）
        _motor.Tick(Time.deltaTime, _groundDetector);
        
        // 应用到 Unity（Transform 是权威源）
        Vector3 movement = _motor.CalculateMovement(Time.deltaTime);
        _controller.Move(movement);
        
        // 可选：同步位置到 CombatEntity（如果需要）
        // _combatEntity.CachedPosition = transform.position;
    }
    
    // 外部注入输入源（用于 AI/网络）
    public void SetInputProvider(IInputProvider provider)
    {
        _inputProvider = provider;
    }
}

// CombatEntity 只读同步
public class CombatEntity
{
    public CharacterMotor Motor { get; private set; }
    public Vector3 CachedPosition { get; internal set; } // 可选缓存
    
    public void Tick(float deltaTime)
    {
        if (IsDead)
            return;

        // 更新战斗逻辑（技能、效果、属性）
        AbilitySystem.Tick(deltaTime);

        // 只读同步移动状态（不驱动 Motor）
        if (Motor != null)
        {
            // 可以读取状态用于战斗逻辑
            bool isGrounded = Motor.IsGrounded;
            MovementState state = Motor.CurrentState;
            
            // 但不调用 Motor.Tick()（由 Bridge 负责）
        }

        // 生命回复等其他逻辑
        float regen = HealthAttributes.GetHealthRegen();
        if (regen > 0f)
        {
            Heal(regen * deltaTime);
        }
    }
}
```

### 地面检测策略

**决策：** 使用自定义 SphereCast 检测，不依赖 `CharacterController.isGrounded`

**理由：**
- Unity 的 `CharacterController.isGrounded` 不够精确，容易在斜坡上抖动
- 自定义检测可以获取地面法线，用于斜坡计算
- 可以控制检测距离和容差，避免状态抖动

**实现：**
```csharp
public class GroundDetector : IGroundDetector
{
    private CharacterControllerConfig _config;
    
    public bool IsGrounded(Vector3 position, out RaycastHit hit)
    {
        // 使用 SphereCast 从角色底部向下检测
        Vector3 origin = position + Vector3.up * _config.GroundCheckRadius;
        float distance = _config.GroundCheckDistance + _config.GroundCheckRadius;
        
        return Physics.SphereCast(
            origin, 
            _config.GroundCheckRadius, 
            Vector3.down, 
            out hit, 
            distance, 
            _config.GroundLayers
        );
    }
    
    public Vector3 GetGroundNormal(RaycastHit hit)
    {
        return hit.normal;
    }
    
    public float GetSlopeAngle(Vector3 normal)
    {
        return Vector3.Angle(Vector3.up, normal);
    }
}
```

**冲突处理：**
- 忽略 `CharacterController.isGrounded`，只使用自定义检测结果
- 检测频率：每帧一次（在 Motor.Tick 中调用）
- 容差设置：GroundCheckDistance = 0.2m（避免跳跃后立即判定为地面）

**单元测试（不需要 Unity）：**
- `CharacterMotor` 的移动计算
- `MovementStateMachine` 的状态转换
- 属性修改器对速度的影响

**集成测试（PlayMode）：**
- 角色在场景中移动
- 地面检测和斜坡行走
- 标签和效果对移动的影响

**性能测试：**
- 100 个角色同时移动的帧率
- 内存分配和 GC 压力
