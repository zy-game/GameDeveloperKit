## ADDED Requirements

### Requirement: Combat Data Assets (Roguelike)
系统 SHALL 提供一套符合Roguelike游戏特点的战斗数据资源，包含20个技能和相应的效果数据，支持Build构建和词缀叠加。

#### Scenario: 技能数据存储
- **WHEN** 访问 Assets/Data/Abilities/ 目录
- **THEN** 包含20个 AbilityBase ScriptableObject 资源
- **AND** 技能设计符合Roguelike特点（可堆叠、条件触发、风险回报）

#### Scenario: 效果数据存储
- **WHEN** 访问 Assets/Data/Effects/ 目录
- **THEN** 包含所有技能所需的 GameplayEffect ScriptableObject 资源
- **AND** 效果支持堆叠机制（Stack策略）

---

### Requirement: Basic Attack Abilities
系统 SHALL 提供3个基础攻击技能，作为战斗的核心输出手段。

#### Scenario: 斩击 (Slash)
- **WHEN** 激活斩击技能
- **THEN** 对目标造成物理伤害
- **AND** 冷却时间为0.5秒
- **AND** 无消耗
- **AND** 可被其他技能动画取消

#### Scenario: 穿刺 (Pierce)
- **WHEN** 激活穿刺技能
- **THEN** 对直线上的敌人造成物理伤害（穿透）
- **AND** 冷却时间为1秒
- **AND** 消耗10点能量

#### Scenario: 重劈 (Cleave)
- **WHEN** 激活重劈技能
- **THEN** 对前方扇形范围敌人造成物理伤害
- **AND** 附加击退效果
- **AND** 冷却时间为1秒
- **AND** 消耗15点能量

---

### Requirement: Elemental Abilities
系统 SHALL 提供4个元素技能，支持构建元素流派Build。

#### Scenario: 火焰弹 (FireBolt)
- **WHEN** 激活火焰弹技能
- **THEN** 对目标造成火焰伤害
- **AND** 叠加灼烧效果（DoT，可堆叠）
- **AND** 冷却时间为1秒
- **AND** 消耗15点法力
- **AND** 授予 Ability.Element.Fire 标签

#### Scenario: 冰霜新星 (FrostNova)
- **WHEN** 激活冰霜新星技能
- **THEN** 对周围敌人造成冰霜伤害
- **AND** 附加冰冻效果（减速80%持续1.5秒）
- **AND** 冷却时间为3秒
- **AND** 消耗25点法力
- **AND** 授予 Ability.Element.Ice 标签

#### Scenario: 闪电链 (ChainLightning)
- **WHEN** 激活闪电链技能
- **THEN** 对目标造成闪电伤害
- **AND** 弹射到附近最多3个敌人
- **AND** 冷却时间为3秒
- **AND** 消耗20点法力
- **AND** 授予 Ability.Element.Lightning 标签

#### Scenario: 毒雾 (PoisonCloud)
- **WHEN** 激活毒雾技能
- **THEN** 在目标位置创建毒雾区域
- **AND** 区域内敌人持续受到毒素伤害（DoT，可堆叠）
- **AND** 持续5秒
- **AND** 冷却时间为8秒
- **AND** 消耗30点法力
- **AND** 授予 Ability.Element.Poison 标签

---

### Requirement: Mobility Abilities
系统 SHALL 提供3个机动技能，作为Roguelike生存的核心。

#### Scenario: 翻滚 (Roll)
- **WHEN** 激活翻滚技能
- **THEN** 向移动方向快速位移
- **AND** 位移期间获得无敌效果（0.3秒）
- **AND** 冷却时间为1秒
- **AND** 消耗10点能量
- **AND** 授予 State.Invincible 标签（短暂）

#### Scenario: 冲刺斩 (DashStrike)
- **WHEN** 激活冲刺斩技能
- **THEN** 向前冲刺并对路径上敌人造成伤害
- **AND** 冷却时间为3秒
- **AND** 消耗20点能量
- **AND** 授予 State.Movement.Dashing 标签

#### Scenario: 后跳 (Backstep)
- **WHEN** 激活后跳技能
- **THEN** 快速向后位移
- **AND** 可取消攻击硬直
- **AND** 冷却时间为0.5秒
- **AND** 消耗5点能量

---

### Requirement: Enhancement Abilities
系统 SHALL 提供4个增强技能，作为Build构建的核心。

#### Scenario: 嗜血 (Bloodlust)
- **WHEN** 激活嗜血技能
- **THEN** 获得嗜血状态持续15秒
- **AND** 击杀敌人时回复5%最大生命
- **AND** 每次击杀叠加10%攻速（最多5层）
- **AND** 冷却时间为8秒
- **AND** 消耗20点能量
- **AND** 授予 State.Buff.Bloodlust 标签

#### Scenario: 狂战士之怒 (BerserkerRage)
- **WHEN** 激活狂战士之怒技能
- **THEN** 获得狂战士状态持续10秒
- **AND** 伤害加成 = (1 - 当前生命/最大生命) * 100%
- **AND** 生命越低伤害越高（风险回报）
- **AND** 冷却时间为8秒
- **AND** 消耗15点能量
- **AND** 授予 State.Buff.Berserker 标签

#### Scenario: 护盾冲击 (ShieldBash)
- **WHEN** 激活护盾冲击技能且拥有护盾值
- **THEN** 消耗当前护盾值
- **AND** 造成等于消耗护盾值的伤害
- **AND** 附加眩晕效果1秒
- **AND** 冷却时间为3秒
- **AND** 无能量消耗

#### Scenario: 蓄力 (Charge)
- **WHEN** 激活蓄力技能
- **THEN** 进入蓄力状态
- **AND** 蓄力期间无法移动
- **AND** 下次攻击伤害 = 基础伤害 * (1 + 蓄力时间 * 0.5)，最高3倍
- **AND** 蓄力可被打断
- **AND** 冷却时间为1秒
- **AND** 消耗10点能量
- **AND** 授予 State.Charging 标签

---

### Requirement: Conditional Trigger Abilities
系统 SHALL 提供3个条件触发技能，增加战斗深度。

#### Scenario: 反击 (Counter)
- **WHEN** 激活反击技能
- **THEN** 进入反击姿态持续2秒
- **AND** 期间受到攻击时获得反击标记
- **AND** 反击标记下次攻击伤害+50%
- **AND** 冷却时间为3秒
- **AND** 消耗15点能量
- **AND** 授予 State.Counter 标签

#### Scenario: 处决 (Execute)
- **WHEN** 激活处决技能且目标生命低于30%
- **THEN** 对目标造成大量伤害
- **AND** 伤害随目标已损失生命百分比增加
- **AND** 冷却时间为8秒
- **AND** 消耗25点能量
- **AND** 需要目标拥有 State.LowHealth 标签

#### Scenario: 连击大师 (ComboMaster)
- **WHEN** 激活连击大师技能
- **THEN** 获得连击状态持续5秒
- **AND** 每次攻击叠加连击层数（最多10层）
- **AND** 每层连击增加5%伤害
- **AND** 5秒内不攻击则层数清零
- **AND** 冷却时间为8秒
- **AND** 消耗20点能量
- **AND** 授予 State.Buff.Combo 标签

---

### Requirement: Risk-Reward Abilities
系统 SHALL 提供3个风险回报技能，体现Roguelike特色。

#### Scenario: 献祭 (Sacrifice)
- **WHEN** 激活献祭技能
- **THEN** 消耗10%当前生命值
- **AND** 下次攻击伤害+100%
- **AND** 冷却时间为3秒
- **AND** 无能量消耗
- **AND** 授予 State.Sacrificed 标签

#### Scenario: 玻璃大炮 (GlassCannon)
- **WHEN** 激活玻璃大炮技能
- **THEN** 最大生命值降低30%持续整场战斗
- **AND** 攻击力提升50%持续整场战斗
- **AND** 效果不可叠加
- **AND** 冷却时间为0秒（一次性）
- **AND** 无消耗
- **AND** 授予 State.GlassCannon 标签

#### Scenario: 赌徒之刃 (GamblersBlade)
- **WHEN** 激活赌徒之刃技能
- **THEN** 获得赌徒状态持续10秒
- **AND** 期间每次攻击伤害随机为0.5x-2x
- **AND** 冷却时间为8秒
- **AND** 消耗15点能量
- **AND** 授予 State.Buff.Gambler 标签
