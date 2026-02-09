# Change: 创建战斗数据资源（20个技能 + 效果）- Roguelike风格

## Why
项目是一个Roguelike游戏，需要一套符合Roguelike特点的战斗数据资源。技能设计需要考虑：
- **Build构建**：技能之间有协同效应，鼓励玩家构建特定流派
- **风险回报**：部分技能有负面效果换取更强收益
- **可堆叠性**：效果可以通过多次获取叠加增强
- **条件触发**：技能效果与角色状态关联

## What Changes
- 在 `Assets/Data/Abilities/` 创建20个技能数据（AbilityBase ScriptableObject）
- 在 `Assets/Data/Effects/` 创建相应的效果数据（GameplayEffect ScriptableObject）
- 技能设计符合Roguelike游戏特点，支持Build构建和词缀叠加

## Impact
- Affected specs: combat
- Affected code: Assets/Data/ 目录
- 新增约40-50个.asset文件

## 技能列表（Roguelike风格）

### 基础攻击技能 (3个)
1. **斩击** (Slash) - 基础近战攻击，可被动画取消
2. **穿刺** (Pierce) - 穿透攻击，可击中多个敌人
3. **重劈** (Cleave) - 前方扇形攻击，击退敌人

### 元素技能 (4个) - 可构建元素Build
4. **火焰弹** (FireBolt) - 造成火焰伤害，叠加灼烧
5. **冰霜新星** (FrostNova) - 范围冰冻减速
6. **闪电链** (ChainLightning) - 在敌人间弹射
7. **毒雾** (PoisonCloud) - 持续毒伤区域

### 机动技能 (3个) - 生存核心
8. **翻滚** (Roll) - 无敌帧闪避，短CD
9. **冲刺斩** (DashStrike) - 位移+攻击
10. **后跳** (Backstep) - 快速后撤，可取消硬直

### 增强技能 (4个) - Build核心
11. **嗜血** (Bloodlust) - 击杀回血，叠加攻速
12. **狂战士之怒** (BerserkerRage) - 生命越低伤害越高（风险回报）
13. **护盾冲击** (ShieldBash) - 消耗护盾值造成伤害
14. **蓄力** (Charge) - 蓄力增伤，可被打断

### 被动触发技能 (3个) - 条件触发
15. **反击** (Counter) - 受击后短时间内攻击附加伤害
16. **处决** (Execute) - 对低血量敌人造成额外伤害
17. **连击大师** (ComboMaster) - 连续攻击叠加伤害加成

### 风险回报技能 (3个) - Roguelike特色
18. **献祭** (Sacrifice) - 消耗生命值大幅提升伤害
19. **玻璃大炮** (GlassCannon) - 降低最大生命，提升攻击力
20. **赌徒之刃** (GamblersBlade) - 随机造成0.5x-2x伤害

## 效果列表（Roguelike风格）

### 伤害效果
- DamageEffect_Physical (物理伤害)
- DamageEffect_Fire (火焰伤害)
- DamageEffect_Ice (冰霜伤害)
- DamageEffect_Lightning (闪电伤害)
- DamageEffect_Poison (毒素伤害)

### 持续伤害效果 (DoT)
- BurnEffect (灼烧 - 每秒火焰伤害，可堆叠)
- PoisonEffect (中毒 - 每秒毒素伤害，可堆叠)
- BleedEffect (流血 - 每秒物理伤害，可堆叠)

### 控制效果
- SlowEffect (减速50%持续3秒)
- FreezeEffect (冰冻 - 减速80%持续1.5秒)
- StunEffect (眩晕1秒)
- KnockbackEffect (击退)

### 增益效果 (可堆叠)
- AttackBuff (攻击+10%，可堆叠5层)
- AttackSpeedBuff (攻速+10%，可堆叠5层)
- MoveSpeedBuff (移速+20%)
- CritBuff (暴击+5%，可堆叠)
- LifestealBuff (吸血效果)

### 风险回报效果
- BerserkerEffect (生命越低伤害越高)
- GlassCannonEffect (最大生命-30%，攻击+50%)
- SacrificeEffect (消耗10%当前生命，伤害+100%)

### 防御效果
- InvincibleEffect (无敌0.3秒)
- DamageReductionEffect (减伤30%)

### 回复效果
- HealOnKillEffect (击杀回血)
- RegenEffect (每秒回血)

### 特殊效果
- ComboStackEffect (连击层数，每层+5%伤害)
- CounterEffect (反击状态，下次攻击+50%伤害)
- ChargeEffect (蓄力状态，伤害随时间增加)

### 冷却效果
- Cooldown_VeryShort (0.5秒)
- Cooldown_Short (1秒)
- Cooldown_Medium (3秒)
- Cooldown_Long (8秒)
