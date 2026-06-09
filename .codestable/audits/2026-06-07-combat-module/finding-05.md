---
doc_type: audit-finding
audit: 2026-06-07-combat-module
finding_id: "arch-drift-05"
nature: arch-drift
severity: P2
confidence: high
suggested_action: cs-refactor
status: fixed
---

# Finding 05：Combat 已实现但架构总入口仍未记录 Combat 子系统

## 速答

CombatModule 设计要求验收后更新 `.codestable/architecture/ARCHITECTURE.md`，但当前架构总入口只有 FileSystem、Download、Event、Resource 等模块，没有 Combat 小节；代码现状和架构档案已经不同步。

## 关键证据

- `.codestable/features/2026-06-04-combat-module/combat-module-design.md:546` 到 `.codestable/features/2026-06-04-combat-module/combat-module-design.md:555` — 设计明确要求验收通过后新增 Combat 子系统、核心类型、底层依赖、匹配语义、固定帧率和边界。
- `.codestable/architecture/ARCHITECTURE.md:11` 到 `.codestable/architecture/ARCHITECTURE.md:141` — 当前模块索引记录 FileSystem、Download、Event、Resource，没有 Combat。
- `Assets/GameDeveloperKit/Runtime/Combat/CombatModule.cs:10` 到 `Assets/GameDeveloperKit/Runtime/Combat/CombatModule.cs:20` — 代码中已经存在 `CombatModule` 和默认 `World`。
- `Assets/GameDeveloperKit/Runtime/Combat/World.cs:11` 到 `Assets/GameDeveloperKit/Runtime/Combat/World.cs:23` — 代码中已经存在 `World`、`EntityManager` 和 `SystemManager`。

## 影响

CodeStable 的 architecture 目录只记系统现状。缺少 Combat 会让后续 feature、issue、audit 对照架构时低估已有模块，也容易重复讨论“Combat 是否是现有能力”。这不影响运行时，但会影响协作和后续审计准确性。

## 修复方向

用 `cs-arch update` 或 feature acceptance 收尾，把 Combat 子系统现状同步到 `.codestable/architecture/ARCHITECTURE.md`。

## 建议动作

`cs-refactor` 或 `cs-arch`；这里更适合直接走 `cs-arch` 更新架构档案。
