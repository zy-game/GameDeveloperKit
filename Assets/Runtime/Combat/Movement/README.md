# Combat Movement System

## 概述

战斗移动系统将移动逻辑完全集成到 `Character` 类中，**Character 直接与 KinematicCharacterMotor 交互，无需 MonoBehaviour 中介**。

**设计原则：Character 包含所有逻辑，直接实现 ICharacterController 接口。**

## 架构

```
[战斗系统 - 数据+逻辑层]
    Character : ICharacterController (纯 C#)
        ↓ 实现移动逻辑 (UpdateVelocity, UpdateRotation)
        ↓ 处理输入 (Tick 中调用 InputProvider.ProcessInput)
        ↓ 持有 MovementAttributeSet
        ↓ 直接赋值给 Motor.CharacterController
        
[KCC 库 - 物理引擎]
    KinematicCharacterMotor : MonoBehaviour
        ↓ 持有 ICharacterController 引用
        ↓ 调用 Character.UpdateVelocity/UpdateRotation
        ↓ 驱动物理
    Unity Transform (位置权威源)
    
[可选组件]
    RootMotionCollector : MonoBehaviour
        ↓ 仅在需要 Root Motion 时添加
        ↓ 收集 Animator.deltaPosition/deltaRotation
```

## 核心组件

### 1. Character (数据+逻辑)
直接实现 `ICharacterController` 接口，包含所有移动逻辑：

**公开方法：**
- `Tick(deltaTime)` - 每帧更新，内部调用 InputProvider.ProcessInput
- `SetMoveInput(Vector3)` - 设置移动输入
- `Jump()` - 请求跳跃
- `SetExternalVelocity(Vector3)` - 设置外部速度（用于冲刺、击退等）
- `ClearExternalVelocity()` - 清除外部速度
- `Teleport(Vector3)` - 瞬移到指定位置
- `ForceUnground()` - 强制离地
- `GetVelocity()` - 获取当前速度

**公开属性：**
- `IsGrounded` - 是否在地面上
- `InputProvider` - 输入提供者
- `MovementAttributes` - 移动属性集
- `Motor` - KinematicCharacterMotor 引用（内部设置）
- `Transform` - Unity Transform 引用（内部设置）
- `Animator` - Unity Animator 引用（可选）
- `RotationSharpness` - 旋转平滑度
- `TerminalVelocity` - 终端速度
- `AirControlFactor` - 空中控制系数
- `UseRootMotion` - 启用 Root Motion
- `RootMotionBlendWeight` - Root Motion 混合权重

**回调：**
- `OnStateChanged` - 状态变化回调（业务层订阅）

### 2. RootMotionCollector (可选组件)
仅在需要 Root Motion 时添加的 MonoBehaviour：

```csharp
public class RootMotionCollector : MonoBehaviour
{
    public Character Character;
    public Animator Animator;
    
    private void OnAnimatorMove()
    {
        // 收集 Root Motion 数据到 Character
        if (Character.UseRootMotion && Animator != null)
        {
            Character.RootMotionPositionDelta += Animator.deltaPosition;
            Character.RootMotionRotationDelta = Animator.deltaRotation * Character.RootMotionRotationDelta;
        }
    }
}
```

### 3. MovementAttributeSet (属性集)
控制移动参数的属性集：
- `MoveSpeed` - 移动速度 (默认: 5.0 m/s)
- `Acceleration` - 加速度 (默认: 10.0)
- `JumpHeight` - 跳跃高度 (默认: 1.5 m)
- `Gravity` - 重力加速度 (默认: -20.0 m/s²)
- `Mass` - 质量 (默认: 70.0 kg)

### 4. IInputProvider (接口)
输入提供者接口，负责处理输入并调用 Character 的方法：
```csharp
public interface IInputProvider
{
    void ProcessInput(Character character, float deltaTime);
}
```

## 使用方法

### 推荐方式：使用 CombatModule 创建角色

```csharp
var config = Resources.Load<CharacterConfig>("Configs/PlayerConfig");
var character = Game.Combat.CreateCharacter(
    config,
    position: Vector3.zero,
    inputProvider: new MyInputProvider()
);
```

### 自定义输入提供者

```csharp
public class MyInputProvider : IInputProvider
{
    public void ProcessInput(Character character, float deltaTime)
    {
        // 完全自定义输入处理
        Vector3 moveDir = GetMoveDirection();
        character.SetMoveInput(moveDir);
        
        if (ShouldJump())
            character.Jump();
        
        // 业务层实现冲刺
        if (ShouldDash())
        {
            character.SetExternalVelocity(dashDirection * dashSpeed);
            // 业务层自己管理冲刺持续时间和结束
        }
    }
}
```

### 业务层实现高级功能

框架层不提供冲刺、击退等功能，业务层通过 `SetExternalVelocity` 实现：

```csharp
// 业务层冲刺实现示例
public class DashAbility
{
    private Character _character;
    private float _dashDuration = 0.3f;
    private float _dashSpeed = 15f;
    private float _elapsed;
    private bool _isDashing;
    
    public void StartDash(Vector3 direction)
    {
        _isDashing = true;
        _elapsed = 0f;
        _character.SetExternalVelocity(direction * _dashSpeed);
    }
    
    public void Update(float deltaTime)
    {
        if (_isDashing)
        {
            _elapsed += deltaTime;
            if (_elapsed >= _dashDuration)
            {
                _isDashing = false;
                _character.ClearExternalVelocity();
            }
        }
    }
}
```

### 订阅状态变化

```csharp
character.OnStateChanged += (character, velocity, isGrounded) =>
{
    // 业务层自己决定状态
    float speed = new Vector2(velocity.x, velocity.z).magnitude;
    
    if (!isGrounded)
        myState = velocity.y > 0 ? MyState.Jumping : MyState.Falling;
    else if (speed < 0.1f)
        myState = MyState.Idle;
    else if (speed > runThreshold)
        myState = MyState.Running;
    else
        myState = MyState.Walking;
};
```

## 数据流向

```
[CombatModule.OnUpdate] → [Character.Tick(deltaTime)]
                ↓ 内部调用
        [InputProvider.ProcessInput(character, deltaTime)]
                ↓ 调用
        [Character.SetMoveInput/Jump/SetExternalVelocity]
                ↓
        [KinematicCharacterMotor.Update]
                ↓ 调用接口
        [Character.UpdateVelocity/UpdateRotation] (ICharacterController)
                ↓ 读取
        [Character.MovementAttributes]
                ↓ 应用物理
        [Transform] (位置权威源)
                ↓ 同步
        [Character.CachedPosition/IsGrounded] (只读)
                ↓ 回调
        [Character.OnStateChanged] (业务层订阅)
```

## 架构优势

1. **Character 是纯 C#** - 可以独立测试，不依赖 Unity
2. **无 MonoBehaviour 中介** - Character 直接与 KinematicCharacterMotor 交互
3. **逻辑完全集中** - 所有移动逻辑和输入处理在 Character 中
4. **输入在 Tick 中** - Character.Tick() 内部调用 InputProvider.ProcessInput
5. **Root Motion 可选** - 只在需要时添加 RootMotionCollector
6. **框架层无状态** - 不定义 MovementState 枚举
7. **框架层无业务逻辑** - 不检查定身、冲刺等状态

## 设计原则

1. **Character 完全自治** - 移动计算、速度更新、旋转处理、输入处理
2. **直接实现接口** - Character 实现 ICharacterController，直接赋值给 Motor
3. **输入在 Tick 中处理** - 不需要 MonoBehaviour.Update
4. **MonoBehaviour 可选** - 只在需要 Root Motion 时才添加
5. **能力而非行为** - 框架层只提供 SetMoveInput、Jump 等能力
6. **业务层自由** - 冲刺、击退、状态机等由业务层实现

## 框架层提供的能力

| 能力 | 方法 | 说明 |
|------|------|------|
| 移动 | SetMoveInput | 设置移动方向 |
| 跳跃 | Jump | 请求跳跃 |
| 外部速度 | SetExternalVelocity | 覆盖正常移动（用于冲刺、击退等） |
| 瞬移 | Teleport | 瞬间移动到目标位置 |
| 强制离地 | ForceUnground | 强制角色离开地面 |

## 业务层需要实现的功能

- 状态机（Idle, Walk, Run, Jump, Dash 等）
- 冲刺系统
- 击退效果
- 定身效果（通过不调用 SetMoveInput 实现）
- 冲锋技能
- 闪现技能

## 测试

使用 `MovementTest` 组件进行基础测试：

1. 创建一个 GameObject
2. 添加 `MovementTest` 组件
3. 运行场景
4. 使用 WASD 移动，空格跳跃
5. 右键点击组件，使用 Context Menu 测试：
   - Test: Apply Slow (50%)
   - Test: Remove Slow
   - Test: External Velocity Forward
   - Test: Teleport Forward 5m

## 文件结构

```
Combat/
├── Core/
│   ├── Character.cs                    # 实现 ICharacterController，包含所有逻辑
│   ├── CharacterConfig.cs              # 角色配置
│   └── CombatModule.cs                 # 创建角色，直接设置 Character 引用
├── Movement/
│   ├── RootMotionCollector.cs          # Root Motion 收集器（可选，~20 行）
│   ├── IInputProvider.cs               # 输入提供者接口
│   ├── MovementAttributeSet.cs         # 移动属性集
│   ├── MovementEffectData.cs           # 移动效果数据（业务层使用）
│   ├── MovementTest.cs                 # 测试脚本
│   └── README.md                       # 本文档
```

## 关键代码示例

### 业务层继承 Character

所有关键方法都标记为 `virtual`，方便业务层重载：

```csharp
public class MyCharacter : Character
{
    public MyCharacter(string name) : base(name) { }
    
    // 重载 Tick，添加自定义逻辑
    public override void Tick(float deltaTime)
    {
        // 自定义逻辑
        UpdateMyCustomLogic(deltaTime);
        
        // 调用基类
        base.Tick(deltaTime);
    }
    
    // 重载速度更新，实现自定义移动逻辑
    public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // 检查自定义状态
        if (IsInMyCustomState)
        {
            currentVelocity = MyCustomVelocity;
            return;
        }
        
        // 调用基类默认逻辑
        base.UpdateVelocity(ref currentVelocity, deltaTime);
    }
    
    // 重载跳跃，添加二段跳
    public override void Jump()
    {
        if (IsGrounded)
        {
            base.Jump();
            _canDoubleJump = true;
        }
        else if (_canDoubleJump)
        {
            base.Jump();
            _canDoubleJump = false;
        }
    }
    
    // 重载碰撞检测
    public override bool IsColliderValidForCollisions(Collider coll)
    {
        // 自定义碰撞过滤逻辑
        if (coll.CompareTag("IgnoreMe"))
            return false;
        
        return base.IsColliderValidForCollisions(coll);
    }
}
```

### CombatModule 创建角色

```csharp
// 创建 GameObject
var go = new GameObject(config.CharacterName);

// 添加 KinematicCharacterMotor
var motor = go.AddComponent<KinematicCharacterMotor>();

// 直接设置 Character 的 Unity 引用
character.Motor = motor;
character.Transform = go.transform;

// 将 Character 设置为 Motor 的控制器（无需 MonoBehaviour 中介）
motor.CharacterController = character;

// 可选：如果需要 Root Motion
if (character.UseRootMotion)
{
    var animator = go.GetComponent<Animator>();
    character.Animator = animator;
    var collector = go.AddComponent<RootMotionCollector>();
    collector.Character = character;
    collector.Animator = animator;
}
```
