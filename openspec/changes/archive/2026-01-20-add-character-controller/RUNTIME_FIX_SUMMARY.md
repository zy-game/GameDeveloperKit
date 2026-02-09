# 运行时错误修复总结

## 修复日期
2026-01-20

## 问题描述
运行时错误：在 `Update()` 方法中调用 GUI 函数

### 错误信息
```
ArgumentException: You can only call GUI functions from inside OnGUI.
UnityEngine.GUIUtility.CheckOnGUI ()
UnityEngine.GUI.Label (UnityEngine.Rect position, System.String text, UnityEngine.GUIStyle style)
GameDeveloperKit.Combat.Tests.MovementTest.DrawDebugInfo ()
GameDeveloperKit.Combat.Tests.MovementTest.Update ()
```

---

## 问题原因

在 `Update()` 方法中调用了 `DrawDebugInfo()`，而该方法内部使用了 `GUI.Label()`，这违反了 Unity 的规则：
- **GUI 函数只能在 `OnGUI()` 中调用**
- `Update()` 是游戏逻辑更新，不能调用 GUI 函数

---

## 修复方案

### 错误代码
```csharp
private void Update()
{
    // ...
    
    // ❌ 错误：在 Update 中调用 DrawDebugInfo
    if (ShowDebugInfo)
    {
        DrawDebugInfo();
    }
}

private void DrawDebugInfo()
{
    // ❌ 错误：在非 OnGUI 方法中使用 GUI.Label
    GUI.Label(new Rect(10, 10, 400, 200), info, style);
}
```

### 修复后代码
```csharp
private void Update()
{
    // 只处理游戏逻辑
    Entity.Tick(Time.deltaTime);
    
    // 处理输入
    if (Input.GetKeyDown(KeyCode.LeftShift))
    {
        controller.Dash(transform.forward);
    }
    
    // ✅ 不再调用 DrawDebugInfo
}

private void DrawDebugInfo()
{
    // ✅ 使用 Debug.DrawRay 代替 GUI.Label
    // Debug.DrawRay 可以在任何地方调用
    var controller = GetComponent<CombatCharacterController>();
    if (controller != null && controller.Motor != null)
    {
        Vector3 velocity = controller.Motor.Velocity;
        Debug.DrawRay(transform.position, velocity, Color.green);
    }
}

private void OnGUI()
{
    // ✅ GUI 调用在 OnGUI 中
    if (ShowDebugInfo && Entity != null)
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        
        var controller = GetComponent<CombatCharacterController>();
        if (controller != null && controller.Motor != null)
        {
            string info = $"Movement State: {Entity.MovementState}\n" +
                         $"Is Grounded: {Entity.IsGrounded}\n" +
                         // ... 更多信息
                         
            GUI.Label(new Rect(10, 10, 400, 450), info, style);
        }
    }
}
```

---

## 修复详情

### 1. 移除 Update 中的 GUI 调用
- ❌ 删除了 `Update()` 中的 `DrawDebugInfo()` 调用
- ✅ `Update()` 现在只处理游戏逻辑和输入

### 2. 简化 DrawDebugInfo
- ❌ 删除了 `GUI.Label()` 调用
- ✅ 改用 `Debug.DrawRay()` 绘制速度向量（可选，目前未使用）

### 3. OnGUI 已经正确实现
- ✅ 所有 GUI 调用都在 `OnGUI()` 方法中
- ✅ 显示完整的调试信息

---

## Unity GUI 规则

### 可以调用 GUI 函数的地方
- ✅ `OnGUI()` 方法
- ✅ 从 `OnGUI()` 调用的方法

### 不能调用 GUI 函数的地方
- ❌ `Update()`
- ❌ `FixedUpdate()`
- ❌ `LateUpdate()`
- ❌ `Start()`
- ❌ `Awake()`
- ❌ 其他生命周期方法

### 替代方案
如果需要在非 `OnGUI()` 方法中绘制调试信息：
- ✅ 使用 `Debug.DrawLine()`
- ✅ 使用 `Debug.DrawRay()`
- ✅ 使用 `Gizmos.DrawXXX()`（在 `OnDrawGizmos()` 中）
- ✅ 使用 TextMeshPro 或 UI Toolkit

---

## 修复验证

### 修复前
```
❌ ArgumentException: You can only call GUI functions from inside OnGUI
```

### 修复后
```
✅ 运行正常，无错误
✅ 调试信息正确显示在屏幕左上角
✅ Update() 只处理游戏逻辑
✅ OnGUI() 处理所有 GUI 显示
```

---

## 最终代码结构

```csharp
public class MovementTest : MonoBehaviour
{
    private void Update()
    {
        // ✅ 游戏逻辑更新
        Entity.Tick(Time.deltaTime);
        
        // ✅ 输入处理
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            controller.Dash(transform.forward);
        }
    }
    
    private void OnGUI()
    {
        // ✅ GUI 显示
        if (ShowDebugInfo && Entity != null)
        {
            // 显示调试信息
            GUI.Label(new Rect(10, 10, 400, 450), info, style);
        }
    }
    
    private void DrawDebugInfo()
    {
        // ✅ 场景调试绘制（可选）
        Debug.DrawRay(transform.position, velocity, Color.green);
    }
}
```

---

## 总结

成功修复了运行时 GUI 错误：
1. ✅ 移除了 `Update()` 中的 GUI 调用
2. ✅ 简化了 `DrawDebugInfo()` 方法
3. ✅ 保持 `OnGUI()` 正确实现
4. ✅ 遵循 Unity GUI 调用规则

**代码现在可以正常运行，调试信息正确显示！** 🎉
