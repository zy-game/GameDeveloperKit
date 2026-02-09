# Phase 2 Implementation Summary

## 实施日期
2026-01-20

## 实施范围
完成了 Phase 2 的高级移动功能，包括冲刺系统、击退效果和位移技能。

---

## 已完成的功能

### 1. 冲刺系统 (Dash System)

#### 新增组件
- ✅ **DashData 类** - 冲刺数据管理
  - 方向、距离、速度
  - 已移动距离追踪
  - 冷却机制
  - 激活状态管理

#### 核心功能
- ✅ `Dash(Vector3 direction)` - 执行冲刺
- ✅ `CanDash` 属性 - 检查是否可以冲刺
- ✅ `IsDashing` 属性 - 检查是否正在冲刺
- ✅ `GetDashCooldownProgress()` - 获取冷却进度（0-1）
- ✅ 冲刺期间忽略其他输入
- ✅ 自动完成检测（距离达到后停止）
- ✅ Dashing 状态添加到 MovementState 枚举

#### 配置参数
```csharp
DashSpeed = 15f;        // 冲刺速度 (m/s)
DashDistance = 5f;      // 冲刺距离 (m)
DashCooldown = 1f;      // 冷却时间 (s)
```

---

### 2. 击退效果 (Knockback Effect)

#### 新增组件
- ✅ **KnockbackData 类** - 击退数据管理
  - 方向、速度、持续时间
  - 已持续时间追踪
  - 激活状态管理
  - 带衰减的速度计算

#### 核心功能
- ✅ `ApplyKnockback(Vector3 direction, float speed, float duration)` - 应用击退
- ✅ `IsKnockedBack` 属性 - 检查是否正在被击退
- ✅ 自动强制离地（`Motor.ForceUnground()`）
- ✅ 线性衰减的击退速度
- ✅ 击退期间禁用输入控制
- ✅ 击退期间仍应用重力

#### 特性
- **最高优先级** - 击退效果优先于所有其他移动
- **自动衰减** - 速度随时间线性减少
- **空中击退** - 自动离地，支持空中击退

---

### 3. 位移技能 (Displacement Abilities)

#### 新增组件
- ✅ **DisplacementData 类** - 位移数据管理
  - 位移类型（Blink/Charge）
  - 目标位置
  - 移动速度（用于 Charge）
  - 激活和完成状态

#### 3.1 闪现 (Blink)
- ✅ `Blink(Vector3 targetPosition)` - 瞬移到目标位置
- ✅ 无碰撞检测
- ✅ 立即完成
- ✅ 直接设置位置（`Motor.SetPosition()`）

#### 3.2 冲锋 (Charge)
- ✅ `Charge(Vector3 targetPosition, float speed)` - 冲锋到目标
- ✅ 带碰撞检测
- ✅ 持续移动直到到达目标
- ✅ 自动完成检测（距离 < 0.1m）
- ✅ 可被障碍物阻挡

#### 通用功能
- ✅ `CancelDisplacement()` - 取消当前位移
- ✅ 防止重复激活（同时只能有一个位移）

---

## 优先级系统

UpdateVelocity 中的处理优先级（从高到低）：

1. **击退效果** - 最高优先级，完全控制移动
2. **冲刺** - 次高优先级，忽略输入
3. **冲锋位移** - 持续移动到目标
4. **定身标签** - 强制速度为 0
5. **正常移动** - 地面/空中移动逻辑

---

## 文件修改

### 修改的文件
```
Assets/Runtime/Combat/Movement/
├── MovementState.cs                   (添加 Dashing 状态)
├── CombatCharacterController.cs       (添加 Phase 2 功能)
├── MovementTest.cs                    (添加 Phase 2 测试)
└── README.md                          (更新文档)
```

### 新增的文件
```
Assets/Runtime/Combat/Movement/
└── MovementEffectData.cs              (3.8 KB - DashData, KnockbackData, DisplacementData)
```

---

## 使用示例

### 冲刺
```csharp
var controller = GetComponent<CombatCharacterController>();

// 执行冲刺
controller.Dash(transform.forward);

// 检查状态
if (controller.CanDash)
{
    // 可以冲刺
}

// 获取冷却进度
float progress = controller.GetDashCooldownProgress(); // 0.0 到 1.0
```

### 击退
```csharp
// 向后击退
controller.ApplyKnockback(
    direction: -transform.forward,
    speed: 10f,
    duration: 0.5f
);

// 检查状态
if (controller.IsKnockedBack)
{
    // 正在被击退
}
```

### 闪现
```csharp
// 向前闪现 5 米
Vector3 targetPos = transform.position + transform.forward * 5f;
controller.Blink(targetPos);
```

### 冲锋
```csharp
// 向前冲锋 10 米
Vector3 targetPos = transform.position + transform.forward * 10f;
controller.Charge(targetPos, speed: 15f);

// 取消冲锋
controller.CancelDisplacement();
```

---

## 测试方法

### 键盘输入测试
- **Shift** - 向前冲刺

### Context Menu 测试
右键点击 MovementTest 组件：
- **Test: Dash Forward** - 向前冲刺
- **Test: Knockback Backward** - 向后击退
- **Test: Blink Forward 5m** - 向前闪现 5 米
- **Test: Charge Forward 10m** - 向前冲锋 10 米

### 调试信息
运行时屏幕左上角显示：
- Can Dash（是否可以冲刺）
- Is Dashing（是否正在冲刺）
- Dash Cooldown（冲刺冷却进度）
- Is Knocked Back（是否正在被击退）

---

## 技术细节

### 冲刺实现
```csharp
// 在 UpdateVelocity 中
if (_dashData.IsActive)
{
    float dashMoveDistance = _dashData.Speed * deltaTime;
    _dashData.TraveledDistance += dashMoveDistance;

    if (_dashData.TraveledDistance >= _dashData.Distance)
    {
        _dashData.IsActive = false; // 完成
    }
    else
    {
        currentVelocity = _dashData.Direction * _dashData.Speed;
        return; // 忽略其他输入
    }
}
```

### 击退实现
```csharp
// 在 UpdateVelocity 中
if (_knockbackData.IsActive)
{
    Vector3 knockbackVelocity = _knockbackData.GetCurrentVelocity();
    currentVelocity = knockbackVelocity;
    
    // 应用重力（如果在空中）
    if (!Motor.GroundingStatus.IsStableOnGround)
    {
        currentVelocity.y += gravity * deltaTime;
    }
    
    return; // 最高优先级
}

// 速度衰减
public Vector3 GetCurrentVelocity()
{
    if (!IsActive) return Vector3.zero;
    
    float t = ElapsedTime / Duration;
    float currentSpeed = Speed * (1f - t); // 线性衰减
    return Direction * currentSpeed;
}
```

### 冲锋实现
```csharp
// 在 UpdateVelocity 中
if (_displacementData.IsActive && 
    _displacementData.Type == DisplacementData.DisplacementType.Charge)
{
    Vector3 toTarget = _displacementData.TargetPosition - transform.position;
    float distanceToTarget = toTarget.magnitude;

    if (distanceToTarget < 0.1f)
    {
        _displacementData.IsCompleted = true;
        _displacementData.IsActive = false;
    }
    else
    {
        Vector3 chargeDirection = toTarget.normalized;
        currentVelocity = chargeDirection * _displacementData.Speed;
        return;
    }
}
```

---

## 与战斗系统集成

### 技能触发示例
```csharp
// 在技能效果中触发冲刺
public class DashAbilityEffect : GameplayEffect
{
    public override void Apply(CombatEntity target)
    {
        var controller = target.Owner as GameObject;
        var charController = controller?.GetComponent<CombatCharacterController>();
        
        if (charController != null)
        {
            Vector3 dashDirection = controller.transform.forward;
            charController.Dash(dashDirection);
        }
    }
}

// 在技能效果中触发击退
public class KnockbackEffect : GameplayEffect
{
    public float KnockbackSpeed = 10f;
    public float KnockbackDuration = 0.5f;
    
    public override void Apply(CombatEntity target)
    {
        var controller = target.Owner as GameObject;
        var charController = controller?.GetComponent<CombatCharacterController>();
        
        if (charController != null)
        {
            Vector3 knockbackDir = (target.CachedPosition - source.CachedPosition).normalized;
            charController.ApplyKnockback(knockbackDir, KnockbackSpeed, KnockbackDuration);
        }
    }
}
```

---

## 性能考虑

- **冲刺** - 每帧计算距离，开销极小
- **击退** - 每帧计算衰减速度，开销极小
- **冲锋** - 每帧计算到目标距离，开销极小
- **闪现** - 一次性操作，无持续开销

所有 Phase 2 功能对性能影响可忽略不计。

---

## 已知限制

1. **冲刺碰撞** - 冲刺期间会被障碍物阻挡（由 KCC 库处理）
2. **击退叠加** - 新的击退会覆盖旧的击退（不叠加）
3. **位移互斥** - 同时只能有一个位移技能激活
4. **冲锋精度** - 到达目标的阈值为 0.1m

---

## 下一步工作

### Phase 3 功能
- [ ] Root Motion 支持
- [ ] 碰撞推挤（使用 Mass 属性）
- [ ] 二段跳
- [ ] 空中冲刺

### 优化和完善
- [ ] 冲刺轨迹预测
- [ ] 击退叠加机制
- [ ] 位移技能队列
- [ ] 更多位移类型（传送门、钩锁等）

---

## 参考文档

- **设计文档**: `openspec/changes/add-character-controller/design.md`
- **规格文档**: `openspec/changes/add-character-controller/specs/combat/spec.md`
- **任务列表**: `openspec/changes/add-character-controller/tasks.md`
- **使用文档**: `Assets/Runtime/Combat/Movement/README.md`
- **MVP 总结**: `openspec/changes/add-character-controller/IMPLEMENTATION_SUMMARY.md`
