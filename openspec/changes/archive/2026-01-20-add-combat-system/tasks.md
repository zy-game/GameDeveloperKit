## 1. 核心模块结构
- [x] 1.1 创建 `Assets/Runtime/Combat/` 目录结构
- [x] 1.2 实现 `ICombatManager` 接口
- [x] 1.3 实现 `CombatModule` 模块类
- [x] 1.4 在 `Game.cs` 中注册 Combat 模块

## 2. 标签系统 (Tags)
- [x] 2.1 实现 `GameplayTag` 结构体
- [x] 2.2 实现 `TagContainer` 标签容器
- [x] 2.3 实现 `TagQuery` 标签查询

## 3. 属性系统 (Attributes)
- [x] 3.1 实现 `AttributeValue` 属性值结构
- [x] 3.2 实现 `AttributeModifier` 修改器
- [x] 3.3 实现 `AttributeSet` 属性集合基类
- [x] 3.4 实现常用属性集（HealthSet, CombatSet, ResourceSet）

## 4. 效果系统 (Effects)
- [x] 4.1 实现 `GameplayEffect` 效果定义
- [x] 4.2 实现 `ActiveEffect` 激活效果
- [x] 4.3 实现 `EffectContainer` 效果容器
- [x] 4.4 实现效果堆叠策略

## 5. 技能系统 (Abilities)
- [x] 5.1 实现 `AbilityBase` 技能基类
- [x] 5.2 实现 `AbilitySpec` 技能实例
- [x] 5.3 实现 `AbilityCost` 技能消耗
- [x] 5.4 实现 `AbilitySystemComponent` 技能系统组件
- [x] 5.5 实现技能冷却管理

## 6. 伤害系统 (Damage)
- [x] 6.1 实现 `DamageType` 伤害类型
- [x] 6.2 实现 `DamageInfo` 伤害信息
- [x] 6.3 实现 `DamageCalculator` 伤害计算器
- [x] 6.4 实现 `IDamageReceiver` 接口

## 7. 表现系统 (Cues)
- [x] 7.1 实现 `CueNotify` 表现通知
- [x] 7.2 实现 `ICueHandler` 处理器接口
- [x] 7.3 实现 `CueManager` 表现管理器

## 8. 事件系统
- [x] 8.1 实现 `CombatEventArgs` 基类
- [x] 8.2 实现 `DamageEventArgs` 伤害事件
- [x] 8.3 实现 `AbilityEventArgs` 技能事件
- [x] 8.4 实现 `EffectEventArgs` 效果事件
- [x] 8.5 实现 `AttributeChangedEventArgs` 属性变化事件

## 9. ECS 集成
- [x] 9.1 实现 `CombatComponent` ECS组件
- [x] 9.2 实现 `CombatUpdateSystem` 战斗更新系统

## 10. 测试与示例
- [ ] 10.1 编写单元测试
- [ ] 10.2 创建示例场景
- [ ] 10.3 编写使用文档
