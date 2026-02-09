# Design Document: 集成 Kinematic Character Controller 到战斗系统

## Context

### 现状分析

**项目已有资源：**
- ✅ 完整的 **Kinematic Character Controller (KCC)** 库（第三方资产）
  - `KinematicCharacterMotor` - 成熟的运动学引擎
  - `ICharacterController` - 标准回调接口
  - 完整的物理功能：地面检测、斜坡/台阶处理、碰撞响应、移动平台支持
  - 示例实现：`ExampleCharacterController`

**战斗系统架构：**
- 纯数据层 `CombatEntity`（不依赖 Unity）
- ECS 组件 `CombatComponent`（Unity MonoBehaviour）
- 属性系统、标签系统、技能系统已完成

**核心挑战：**
1. **架构冲突**：KCC 库是 Unity MonoBehaviour，战斗系统是纯数据层
2. **接口适配**：需要实现 `ICharacterController` 接口桥接两个系统
3. **数据同步**：Transform 位置、移动状态、地面状态需要在两个系统间同步
4. **驱动关系**：避免双重更新（KCC 和 CombatModule 同时驱动）

### 设计目标

**不重新实现 KCC**，而是：
1. **适配器模式**：创建适配器实现 `ICharacterController` 接口
2. **桥接两个系统**：连接 `CombatEntity` 和 `KinematicCharacterMotor`
3. **复用所有物理功能**：地面检测、碰撞、斜坡、台阶等全部由 KCC 库处理
4. **保持架构清晰**：单一驱动源、单向数据流

---

## Architecture Decision

### 1. 适配器架构（基于现有 KCC 库）

```
[战斗系统 - 纯数据层]
    CombatEntity (纯 C#)
        ↓ 持有
    MovementAttributeSet (属性)
    MovementState (状态枚举)
        ↓ 只读同步
        
[适配器层 - Unity 集成]
    CombatCharacterController : MonoBehaviour, ICharacterController
        ↓ 实现接口
        ↓ 桥接
        
[KCC 库 - 物理引擎]
    KinematicCharacterMotor (第三方库)
        ↓ 调用接口回调
        ↓ 驱动物理
    Unity Transform (位置权威源)
```

**关键设计：**
- `CombatCharacterController` 实现 `ICharacterController` 接口
- `KinematicCharacterMotor` 通过接口回调驱动移动（`UpdateVelocity`, `UpdateRotation` 等）
- `CombatEntity` 只读同步状态，不驱动物理
- 所有物理功能（地面检测、碰撞、斜坡）由 KCC 库处理

---

## Key Design Decisions

### 2. ICharacterController 接口实现

KCC 库定义的标准接口：

```csharp
public interface ICharacterController
{
    // 在 Motor 更新前调用
    void BeforeCharacterUpdate(float deltaTime);
    
    // Motor 要求更新速度（每帧调用）
    void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);
    
    // Motor 要求更新旋转（每帧调用）
    void UpdateRotation(ref Quaternion currentRotation, float deltaTime);
    
    // 地面检测完成后调用
    void PostGroundingUpdate(float deltaTime);
    
    // Motor 更新完成后调用
    void AfterCharacterUpdate(float deltaTime);
    
    // 碰撞过滤
    bool IsColliderValidForCollisions(Collider coll);
    
    // 碰撞回调
    void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport);
    void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport);
    void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport);
    void OnDiscreteCollisionDetected(Collider hitCollider);
}
```

**我们的实现：**

```csharp
public class CombatCharacterController : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor;
    public CombatEntity Entity;
    
    private Vector3 _moveInput;
    private bool _jumpRequested;
    
    // 从 CombatEntity 读取属性和标签
    private MovementAttributeSet MovementAttributes => Entity.MovementAttributes;
    private bool IsRooted => Entity.Tags.HasTag("State.Movement.Rooted");
    private bool IsSlowed => Entity.Tags.HasTag("State.Movement.Slowed");
    
    // KCC 回调：更新速度
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // 从 CombatEntity 读取属性
        float moveSpeed = MovementAttributes.MoveSpeed.CurrentValue;
        float acceleration = MovementAttributes.Acceleration.CurrentValue;
        float gravity = MovementAttributes.Gravity.CurrentValue;
        
        // 检查标签影响
        if (IsRooted)
        {
            // 定身：速度强制为 0
            currentVelocity = Vector3.zero;
            return;
        }
        
        // 应用 Slowed 标签影响（通过属性修改器）
        // 注：Slowed 标签通过 AttributeModifier 降低 MoveSpeed，这里直接读取最终值即可
        
        // 地面移动
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            Vector3 targetVelocity = _moveInput * moveSpeed;
            
            // 使用 Acceleration 属性计算插值系数
            float sharpness = acceleration; // 加速度直接作为插值锐度
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-sharpness * deltaTime));
        }
        else
        {
            // 空中：应用重力
            currentVelocity += Vector3.up * gravity * deltaTime;
        }
        
        // 跳跃
        if (_jumpRequested && Motor.GroundingStatus.IsStableOnGround)
        {
            float jumpHeight = MovementAttributes.JumpHeight.CurrentValue;
            float jumpSpeed = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
            currentVelocity.y = jumpSpeed;
            _jumpRequested = false;
        }
    }
    
    // KCC 回调：更新旋转
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_moveInput.sqrMagnitude > 0f)
        {
            Vector3 targetDirection = _moveInput.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Motor.CharacterUp);
            currentRotation = Quaternion.Slerp(currentRotation, targetRotation, 1f - Mathf.Exp(-10f * deltaTime));
        }
    }
    
    // KCC 回调：地面检测完成
    public void PostGroundingUpdate(float deltaTime)
    {
        // 同步地面状态到 CombatEntity
        Entity.IsGrounded = Motor.GroundingStatus.IsStableOnGround;
        
        // 自动管理 Airborne 标签
        if (!Motor.GroundingStatus.IsStableOnGround)
        {
            Entity.Tags.AddTag("State.Movement.Airborne");
        }
        else
        {
            Entity.Tags.RemoveTag("State.Movement.Airborne");
        }
    }
    
    // KCC 回调：更新完成
    public void AfterCharacterUpdate(float deltaTime)
    {
        // 同步位置到 CombatEntity（可选缓存）
        Entity.CachedPosition = transform.position;
        
        // 更新移动状态
        UpdateMovementState();
    }
    
    private void UpdateMovementState()
    {
        if (IsRooted)
        {
            Entity.MovementState = MovementState.Idle;
            return;
        }
        
        if (!Motor.GroundingStatus.IsStableOnGround)
        {
            Entity.MovementState = Motor.Velocity.y > 0 ? MovementState.Jumping : MovementState.Falling;
        }
        else
        {
            float speed = new Vector2(Motor.Velocity.x, Motor.Velocity.z).magnitude;
            if (speed < 0.1f)
                Entity.MovementState = MovementState.Idle;
            else if (speed > 3.0f)
                Entity.MovementState = MovementState.Running;
            else
                Entity.MovementState = MovementState.Walking;
        }
    }
    
    // 输入接口（由外部调用）
    public void SetMoveInput(Vector3 input)
    {
        _moveInput = input;
    }
    
    public void Jump()
    {
        _jumpRequested = true;
    }
    
    // 其他接口方法（简化实现）
    public void BeforeCharacterUpdate(float deltaTime) { }
    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
}
```

**Acceleration 计算说明：**
地面移动速度插值系数采用 `1 - exp(-Acceleration * deltaTime)`，Acceleration 作为速度收敛速度控制参数。

---

### 3. 组件归属和生命周期

**决策：** 扩展现有的 `CombatComponent`（ECS 组件）

```csharp
public class CombatComponent : IComponent
{
    public CombatEntity Entity { get; set; }
    public CombatCharacterController CharacterController { get; set; } // 新增
    
    // CombatComponent 负责自动添加/移除 CombatCharacterController 和 KinematicCharacterMotor
    // CombatCharacterController 在 Awake 中校验并补齐缺失的 KinematicCharacterMotor
}
```

**组件添加责任归属：**
- **CombatComponent**：当 `Entity.EnableMovement()` 被调用时，自动添加 `CombatCharacterController` 和 `KinematicCharacterMotor`
- **CombatCharacterController**：在 `Awake()` 中检查 `KinematicCharacterMotor` 是否存在，如不存在则自动添加（防御性编程）
- **单一原则**：优先由 `CombatComponent` 管理，`CombatCharacterController` 作为备用保障

**生命周期和驱动关系（单一权威源）：**

```
[输入源] → [CombatCharacterController.SetMoveInput] 
                ↓
        [KinematicCharacterMotor.Update]
                ↓ 调用接口
        [ICharacterController.UpdateVelocity/UpdateRotation]
                ↓ 读取
        [CombatEntity.MovementAttributes/Tags]
                ↓ 应用物理
        [Transform] (位置权威源)
                ↓ 同步
        [CombatEntity.CachedPosition/IsGrounded/MovementState] (只读)
```

**明确的职责划分：**
1. **KinematicCharacterMotor（驱动者）**：
   - Unity 自动调用 `Update()`
   - 调用 `ICharacterController` 接口方法
   - 处理所有物理（地面检测、碰撞、斜坡、台阶）
   - 更新 Transform 位置

2. **CombatCharacterController（适配器）**：
   - 实现 `ICharacterController` 接口
   - 从 `CombatEntity` 读取属性和标签
   - 计算速度和旋转（基于战斗系统数据）
   - 同步状态回 `CombatEntity`

3. **CombatEntity（数据层）**：
   - 持有 `MovementAttributeSet`（属性）
   - 持有 `Tags`（影响移动）
   - **只读同步**：`IsGrounded`, `MovementState`, `CachedPosition`
   - **不驱动** Motor（避免双重更新）

4. **CombatModule**：
   - 调用 `CombatEntity.Tick()` 更新战斗逻辑（技能、效果、属性）
   - **不直接驱动** 移动（由 KCC Motor 负责）

**数据流向（单向）：**
```
Transform.position (权威) → CombatEntity.CachedPosition (缓存，可选)
Motor.GroundingStatus → CombatEntity.IsGrounded (只读)
Motor.Velocity → CombatEntity.MovementState (只读)
CombatEntity.Tags → Motor (读取，影响移动)
CombatEntity.Attributes → Motor (读取，影响速度)
```

**避免的反模式：**
- ❌ CombatModule 和 Motor 同时驱动移动（双重更新）
- ❌ CombatEntity.Tick() 调用 Motor 方法（与 KCC 冲突）
- ❌ 直接修改 Transform（破坏 KCC 的物理计算）

---

### 4. 属性集设计

**决策：** 创建独立的 `MovementAttributeSet`，不与 `CombatAttributeSet` 合并

**理由：**
- 移动属性是可选的（不是所有 CombatEntity 都需要移动）
- 避免污染战斗属性命名空间
- 便于单独测试和调整

```csharp
public class MovementAttributeSet : AttributeSet
{
    // 基础移动
    public AttributeValue MoveSpeed { get; private set; }      // 默认: 5.0 m/s
    public AttributeValue Acceleration { get; private set; }   // 默认: 10.0（用作插值锐度系数）
    
    // 跳跃和重力
    public AttributeValue JumpHeight { get; private set; }     // 默认: 1.5 m
    public AttributeValue Gravity { get; private set; }        // 默认: -20.0 m/s²
    
    // 物理参数（Phase 3 才使用）
    public AttributeValue Mass { get; private set; }           // 默认: 70.0 kg（用于推挤，MVP 不使用）
}
```

**Acceleration 属性说明：**
- **用途**：控制角色加速到目标速度的快慢（插值锐度）
- **计算方式**：`sharpness = Acceleration`，用于 `Lerp(current, target, 1 - Exp(-sharpness * deltaTime))`
- **效果**：值越大，加速越快；值越小，加速越慢（更平滑）
- **默认值 10.0**：提供快速但平滑的加速体验

**默认值来源：**
- 参考 KCC 示例（`ExampleCharacterController`）
- 参考 Unity 标准角色控制器
- 可通过配置文件或 ScriptableObject 覆盖

---

### 5. KCC 库配置

**决策：** 使用 KCC 库的默认配置，根据需要调整

**KinematicCharacterMotor 关键参数：**

```csharp
// Capsule Settings（胶囊体设置）
CapsuleRadius = 0.5f
CapsuleHeight = 2.0f
CapsuleYOffset = 1.0f

// Grounding Settings（地面检测）
MaxStableSlopeAngle = 60f  // 最大稳定斜坡角度
StableGroundLayers = -1    // 可站立的层级
GroundDetectionExtraDistance = 0f

// Step Settings（台阶处理）
StepHandling = StepHandlingMethod.Standard
MaxStepHeight = 0.5f       // 最大可爬台阶高度
AllowSteppingWithoutStableGrounding = false

// Ledge Settings（边缘处理）
LedgeAndDenivelationHandling = true
MaxStableDistanceFromLedge = 0.5f

// Rigidbody Interaction（刚体交互）
InteractiveRigidbodyHandling = true
RigidbodyInteractionType = RigidbodyInteractionType.Kinematic
SimulatedCharacterMass = 1f
```

**MVP 阶段调整：**
- `MaxStableSlopeAngle = 45f`（与设计文档一致）
- `MaxStepHeight = 0.3f`（与设计文档一致）
- 其他保持默认值

---

### 6. 移动状态机

**决策：** 简化状态机，由 `CombatCharacterController` 管理

```csharp
public enum MovementState
{
    Idle = 0,
    Walking = 1,
    Running = 2,
    Jumping = 3,
    Falling = 4
    // Dashing = 5  // Phase 2 才添加，MVP 不包含
}
```

**状态转换规则：**
```csharp
private void UpdateMovementState()
{
    // 最高优先级：Rooted 标签强制 Idle
    if (Entity.Tags.HasTag("State.Movement.Rooted"))
    {
        Entity.MovementState = MovementState.Idle;
        return;
    }
    
    // 空中状态
    if (!Motor.GroundingStatus.IsStableOnGround)
    {
        Entity.MovementState = Motor.Velocity.y > 0 ? MovementState.Jumping : MovementState.Falling;
        return;
    }
    
    // 地面状态
    float horizontalSpeed = new Vector2(Motor.Velocity.x, Motor.Velocity.z).magnitude;
    if (horizontalSpeed < 0.1f)
        Entity.MovementState = MovementState.Idle;
    else if (horizontalSpeed > 3.0f)  // RunThreshold
        Entity.MovementState = MovementState.Running;
    else
        Entity.MovementState = MovementState.Walking;
}
```

---

### 7. 输入抽象

**决策：** 支持外部输入注入（AI/网络兼容）

```csharp
public interface IInputProvider
{
    Vector3 GetMoveInput();
    bool GetJumpInput();
}

// 默认实现（用于测试）
public class UnityInputProvider : IInputProvider
{
    public Vector3 GetMoveInput()
    {
        return new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
    }
    
    public bool GetJumpInput()
    {
        return Input.GetButtonDown("Jump");
    }
}

// CombatCharacterController 支持注入
public class CombatCharacterController : MonoBehaviour, ICharacterController
{
    private IInputProvider _inputProvider;
    
    public void SetInputProvider(IInputProvider provider)
    {
        _inputProvider = provider;
    }
    
    private void Update()
    {
        if (_inputProvider != null)
        {
            SetMoveInput(_inputProvider.GetMoveInput());
            if (_inputProvider.GetJumpInput())
                Jump();
        }
    }
}
```

---

### 8. 地面检测策略

**决策：** 完全依赖 KCC 库的地面检测，不自定义

**理由：**
- KCC 库已经实现了完善的地面检测（SphereCast + 稳定性评估）
- 避免重复实现和潜在冲突
- 通过 `Motor.GroundingStatus` 获取所有地面信息

```csharp
// KCC 提供的地面状态
public struct CharacterGroundingReport
{
    public bool FoundAnyGround;          // 是否检测到地面
    public bool IsStableOnGround;        // 是否稳定站立
    public Vector3 GroundNormal;         // 地面法线
    public Vector3 InnerGroundNormal;    // 内部法线
    public Vector3 OuterGroundNormal;    // 外部法线
    public Collider GroundCollider;      // 地面碰撞体
    public Vector3 GroundPoint;          // 接触点
    public bool SnappingPrevented;       // 是否阻止吸附
}

// 使用方式
public void PostGroundingUpdate(float deltaTime)
{
    // 直接使用 KCC 的检测结果
    bool isGrounded = Motor.GroundingStatus.IsStableOnGround;
    Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
    
    // 同步到 CombatEntity
    Entity.IsGrounded = isGrounded;
}
```

---

## MVP Scope Definition

### Phase 1 (MVP) - 核心移动功能

**必须完成：**
1. ✅ 实现 `CombatCharacterController`（适配器）
2. ✅ 实现 `ICharacterController` 接口
3. ✅ 集成 `KinematicCharacterMotor`
4. ✅ 基础移动（WASD）、跳跃、重力
5. ✅ 状态机（Idle/Walk/Run/Jump/Fall）
6. ✅ 标签影响（Rooted 阻止移动，Slowed 降低速度）
7. ✅ 属性系统集成（MoveSpeed, JumpHeight, Gravity）
8. ✅ 输入抽象（支持 AI/网络）

**KCC 库提供（无需实现）：**
- ✅ 地面检测（SphereCast + 稳定性评估）
- ✅ 斜坡处理（≤45°）
- ✅ 台阶爬升（≤0.3m）
- ✅ 碰撞响应和滑动
- ✅ 移动平台支持

**验收标准：**
1. 角色移动速度符合 `MoveSpeed` 属性（误差 < 5%）
2. 跳跃高度符合 `JumpHeight` 属性（误差 < 10%）
3. 重力正确应用，落地检测准确
4. 斜坡和台阶行为符合 KCC 库预期
5. `Rooted` 标签阻止移动
6. `Slowed` 效果降低速度
7. 状态机正确反映移动状态
8. 数据流向单向：Transform → CachedPosition，无回滚

---

### Phase 2 - 高级特性（延后）

- 击退效果（通过 `Motor.ForceUnground()` 和速度叠加）
- 位移技能（闪现、冲锋）
- 冲刺系统（Dashing 状态）
- 碰撞推挤（使用 `SimulatedCharacterMass`）

### Phase 3 - 动画集成（延后）

- Root Motion 支持（KCC 库已支持）
- 二段跳、空中控制
- 动画驱动移动

---

## Non-Goals (明确不做的事)

1. ❌ **不重新实现地面检测**：完全使用 KCC 库的实现
2. ❌ **不自定义碰撞处理**：使用 KCC 库的碰撞系统
3. ❌ **不实现物理引擎**：所有物理由 KCC 库处理
4. ❌ **不修改 KCC 库源码**：只通过接口集成
5. ❌ **MVP 不包含**：Root Motion、推挤、击退、位移技能

---

## Risk Mitigation

### 风险 1：KCC 库版本更新

**风险：** 第三方库更新可能破坏接口兼容性

**缓解：**
- 锁定 KCC 库版本（在 package.json 或版本控制中）
- 创建适配器层隔离变化
- 编写集成测试覆盖关键接口

### 风险 2：性能问题

**风险：** 每帧从 CombatEntity 读取属性可能有性能开销

**缓解：**
- 缓存属性值，只在变化时更新
- 使用 Profiler 监控性能
- 必要时使用对象池

### 风险 3：状态同步延迟

**风险：** CombatEntity 和 Motor 状态可能不同步

**缓解：**
- 使用 `PostGroundingUpdate` 和 `AfterCharacterUpdate` 回调立即同步
- 明确 Transform 是位置权威源
- 避免双向同步

---

## Testing Strategy

**单元测试（不需要 Unity）：**
- `MovementAttributeSet` 属性计算
- 标签影响逻辑（Rooted, Slowed）
- 状态转换规则

**集成测试（PlayMode）：**
- `CombatCharacterController` 与 `KinematicCharacterMotor` 集成
- 地面检测、斜坡、台阶行为
- 标签和属性对移动的影响
- 输入注入（AI/网络模拟）

**验收测试：**
- 对照 8 个验收标准逐项验证
- 性能测试（100+ 角色同时移动）
- 边缘情况测试（极端斜坡、快速移动）

---

## Migration Plan

### 阶段 1：准备工作
1. 确认 KCC 库版本和配置
2. 阅读 KCC 文档和示例
3. 创建测试场景

### 阶段 2：核心实现
1. 实现 `CombatCharacterController`（适配器）
2. 实现 `ICharacterController` 接口方法
3. 集成 `MovementAttributeSet`
4. 实现标签影响逻辑

### 阶段 3：集成测试
1. 创建测试场景（平地、斜坡、台阶）
2. 验证移动速度和跳跃高度
3. 测试标签和属性影响
4. 性能测试

### 阶段 4：文档和示例
1. 编写 API 文档
2. 创建使用示例
3. 更新战斗系统文档

---

## References

- KCC 库文档：`Assets/KinematicCharacterController/UserGuide.pdf`
- KCC 示例：`Assets/KinematicCharacterController/ExampleCharacter/`
- 战斗系统规格：`openspec/specs/combat/spec.md`
