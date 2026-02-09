# API 修复总结

## 修复日期
2026-01-20

## 问题描述
编译错误：使用了错误的 API 调用方式

### 错误类型
1. **标签 API** - 使用了 `Tags` 而不是 `OwnedTags`
2. **属性修改器 API** - 使用了错误的构造函数和方法签名
3. **枚举名称** - 使用了 `ModifierOperation` 而不是 `ModifierOp`

---

## 修复内容

### 1. 标签 API 修复

#### 错误用法
```csharp
Entity.AbilitySystem.Tags.AddTag("State.Movement.Rooted");
Entity.AbilitySystem.Tags.RemoveTag("State.Movement.Rooted");
Entity.AbilitySystem.Tags.HasTag("State.Movement.Rooted");
```

#### 正确用法
```csharp
Entity.AbilitySystem.OwnedTags.AddTag("State.Movement.Rooted");
Entity.AbilitySystem.OwnedTags.RemoveTag("State.Movement.Rooted");
Entity.AbilitySystem.OwnedTags.HasTag("State.Movement.Rooted");
```

**修复位置:**
- `CombatCharacterController.cs` - 3 处
- `MovementTest.cs` - 6 处

---

### 2. 属性修改器 API 修复

#### 错误用法
```csharp
// 错误的构造函数
var modifier = new AttributeModifier(
    "TestSlow",
    ModifierOperation.Multiply,
    0.5f,
    0
);

// 错误的方法签名
Entity.MovementAttributes.AddModifier(MovementAttributeSet.MoveSpeed, modifier);
Entity.MovementAttributes.RemoveModifier(MovementAttributeSet.MoveSpeed, "TestSlow");
```

#### 正确用法
```csharp
// 使用静态工厂方法
var modifier = AttributeModifier.Create(
    MovementAttributeSet.MoveSpeed,  // 属性名
    ModifierOp.Multiply,              // 操作类型
    0.5f,                             // 值
    0,                                // 优先级
    this                              // 来源对象
);

// 正确的方法签名
Entity.MovementAttributes.AddModifier(modifier);
Entity.MovementAttributes.RemoveModifiersFromSource(this);
```

**修复位置:**
- `MovementTest.cs` - 6 处（3 个修改器创建 + 3 个方法调用）

---

### 3. 枚举名称修复

#### 错误用法
```csharp
ModifierOperation.Multiply
ModifierOperation.Add
```

#### 正确用法
```csharp
ModifierOp.Multiply
ModifierOp.Add
ModifierOp.PercentAdd
ModifierOp.Override
```

**修复位置:**
- `MovementTest.cs` - 3 处

---

## 修复后的代码示例

### 定身效果
```csharp
// 应用定身
Entity.AbilitySystem.OwnedTags.AddTag("State.Movement.Rooted");

// 移除定身
Entity.AbilitySystem.OwnedTags.RemoveTag("State.Movement.Rooted");

// 检查定身
bool isRooted = Entity.AbilitySystem.OwnedTags.HasTag("State.Movement.Rooted");
```

### 减速效果
```csharp
// 应用减速
var modifier = AttributeModifier.Create(
    MovementAttributeSet.MoveSpeed,
    ModifierOp.Multiply,
    0.5f,  // 50% 速度
    0,
    this
);
Entity.MovementAttributes.AddModifier(modifier);
Entity.AbilitySystem.OwnedTags.AddTag("State.Movement.Slowed");

// 移除减速
Entity.MovementAttributes.RemoveModifiersFromSource(this);
Entity.AbilitySystem.OwnedTags.RemoveTag("State.Movement.Slowed");
```

### 质量修改
```csharp
// 增加质量
var modifier = AttributeModifier.Create(
    MovementAttributeSet.Mass,
    ModifierOp.Multiply,
    2f,  // 质量翻倍
    0,
    this
);
Entity.MovementAttributes.AddModifier(modifier);

// 重置质量（移除所有来自 this 的修改器）
Entity.MovementAttributes.RemoveModifiersFromSource(this);
```

---

## 修复的文件

1. **CombatCharacterController.cs**
   - 修复 3 处标签 API 调用
   - `IsRooted()` 方法
   - `PostGroundingUpdate()` 方法（2 处）

2. **MovementTest.cs**
   - 修复 6 处标签 API 调用
   - 修复 6 处属性修改器 API 调用
   - 修复 3 处枚举名称

---

## 验证

### 编译状态
✅ 所有编译错误已修复

### 修复验证
- ✅ 标签 API 使用 `OwnedTags`
- ✅ 属性修改器使用 `AttributeModifier.Create()`
- ✅ 枚举使用 `ModifierOp`
- ✅ 方法签名正确

---

## 正确的 API 参考

### AbilitySystemComponent
```csharp
public class AbilitySystemComponent
{
    public TagContainer OwnedTags { get; }  // ✅ 正确
    // public TagContainer Tags { get; }    // ❌ 不存在
}
```

### AttributeModifier
```csharp
public class AttributeModifier
{
    // ✅ 正确：使用静态工厂方法
    public static AttributeModifier Create(
        string attributeName,
        ModifierOp op,
        float value,
        int priority = 0,
        object source = null
    );
    
    // ❌ 错误：没有公共构造函数
    // public AttributeModifier(string name, ModifierOp op, float value, int priority);
}
```

### AttributeSet
```csharp
public abstract class AttributeSet
{
    // ✅ 正确
    public void AddModifier(AttributeModifier modifier);
    public bool RemoveModifier(AttributeModifier modifier);
    public void RemoveModifiersFromSource(object source);
    
    // ❌ 错误：不存在这些重载
    // public void AddModifier(string attributeName, AttributeModifier modifier);
    // public void RemoveModifier(string attributeName, string modifierName);
}
```

### ModifierOp 枚举
```csharp
public enum ModifierOp  // ✅ 正确名称
{
    Add,         // 加法
    PercentAdd,  // 百分比加成
    Multiply,    // 乘法
    Override     // 覆盖
}

// ❌ 错误：不存在
// public enum ModifierOperation { ... }
```

---

## 总结

成功修复了所有 API 调用错误，确保代码使用正确的：
1. ✅ 标签容器 API（`OwnedTags`）
2. ✅ 属性修改器工厂方法（`AttributeModifier.Create()`）
3. ✅ 修改器操作枚举（`ModifierOp`）
4. ✅ 属性集方法签名（`AddModifier(modifier)`）

所有代码现在应该可以正常编译和运行！🎉
