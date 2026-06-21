---
doc_type: audit-finding
id: F03
severity: P1
nature: bug
confidence: high
suggested_action: cs-issue
---

# F03 Register 没有校验所有跳转目标存在

## 证据

- `ValidateChoiceStep()` 只校验 choice target 非空，不校验 target chapter/step 是否存在：`Assets/GameDeveloperKit/Runtime/Story/StoryModule.Program.Validation.cs:151`
- `ValidateBranchStep()` / `ValidateJumpStep()` 也只校验 target 非空：`Assets/GameDeveloperKit/Runtime/Story/StoryModule.Program.Validation.cs:276`
- `ValidateCommandStep()` 校验 command schema 和参数，但没有校验 command outcome target 或 fallback target 是否存在：`Assets/GameDeveloperKit/Runtime/Story/StoryModule.Program.Validation.cs:180`
- 播放时 `JumpTo()` / `EnterStep()` / `GetStep()` 才会因缺失 chapter/step 抛错：`Assets/GameDeveloperKit/Runtime/Story/Runtime/StoryRunner.cs:993`

## 影响

编辑器 compiler 会挡住一部分坏边，但 `StoryModule.Register(StoryProgram)` 是公开运行时入口。外部加载 JSON、热更新数据或代码构造 `StoryProgram` 时，坏 target 可以注册成功，直到玩家播放到该路径才崩。

## 建议

在 `ValidateProgram()` 构建完整 chapter/step map 后，统一校验所有 `StoryTarget`：

- Line target。
- Choice target。
- Command outcome targets 和 fallback target。
- Branch/Jump target。
- Wait target。
- Merge target。

并增加“注册时拒绝缺失 chapter/step target”的 Runtime tests。

