---
doc_type: feature-acceptance
feature: 2026-06-22-ui-window-partial-codegen
status: passed
accepted_at: 2026-06-22
design: .codestable/features/2026-06-22-ui-window-partial-codegen/ui-window-partial-codegen-design.md
roadmap:
roadmap_item:
tags: [ui, editor, codegen, window, partial]
---

# UI Window Partial Codegen 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-22
> 关联方案 doc：`.codestable/features/2026-06-22-ui-window-partial-codegen/ui-window-partial-codegen-design.md`

## 1. 接口契约核对

- [x] `UIDocumentGenerator.Generate()` 输出契约已从旧四件套收敛为 logic partial、design partial、nested model partial：`Assets/GameDeveloperKit/Editor/UI/UIDocumentGenerator.cs` 生成 `{Window}.cs`、`{Window}.Design.g.cs`、`{FileStem}.Model.g.cs`。
- [x] `ResolveNames()` 保留合法窗口类型名：`UI_ExampleWindow` 仍作为 C# 类型名，`UIDocumentInspector.GetClassName()` 对合法 prefab 名不再去掉下划线。
- [x] `UIExampleWindow.Model.g.cs` 文件名与实现一致：验收中发现 design 仍写 `UI_ExampleWindow.Model.g.cs`，已回填为实际实现的 `UIExampleWindow.Model.g.cs`，类型仍是 `UI_ExampleWindow.Model`。
- [x] `UIDocumentGenerator.Templates.cs` 已承载模板输出：`GenerateLogic()`、`GenerateDesign()`、`GenerateNestedModel()` 分离出逻辑骨架、设计绑定和内嵌模型模板。
- [x] `Bindings` 属性落地：design partial 生成 `public Model Bindings { get; private set; }`，避免嵌套类型 `Model` 与实例属性同名冲突。
- [x] Loading 示例已迁移：旧 `LoadingWindow.g.cs` / `LoadingModel.g.cs` / `LoadingModule.g.cs` / `LoadingController.cs` 删除，新 `LoadingWindow.cs`、`LoadingWindow.Design.g.cs`、`LoadingWindow.Model.g.cs` 使用同一个 `LoadingWindow` partial 类型。

## 2. 行为与决策核对

- [x] 业务逻辑文件只首次创建：`UIDocumentGenerator.Generate()` 仅在 logic path 不存在时写入 `GenerateLogic()`，后续生成只覆盖 `.Design.g.cs` / `.Model.g.cs`。
- [x] per-window Module / Controller 停止生成：生成器不再包含 `GenerateModule()` / `GenerateController()`，测试确认 `SampleModule.g.cs` 和 `SampleController.cs` 不存在。
- [x] 顶层 model 停止生成：`GenerateNestedModel()` 输出 `public sealed partial class {WindowName} { public sealed class Model { ... } }`。
- [x] 生命周期接线符合方案：logic skeleton 的 `OnAwakeAsync()` 调用 `InitializeDesignAsync()`；`Release()` 调用 `ReleaseDesign()` 后再 `base.Release()`。
- [x] 本地化仍封装在 design partial：有 `LocalizedTexts` 时生成 `RefreshLocalization()`、`App.Localization.GetText()`、`LocaleChanged` 订阅和取消订阅；无本地化绑定时不引用 `GameDeveloperKit.Localization`。
- [x] 错误语义保持：空 class/output/uiPath、非法字段名、重复字段、缺失组件、本地化组件未选择或类型不支持仍在生成阶段抛出明确异常。
- [x] 范围守护通过：`UIDocument`、`UIBindMapping`、`UILocalizedTextBinding` 序列化字段无 diff；`UIModule` 无 diff；未新增 Addressables、UI Toolkit runtime、动画框架或第三方依赖。
- [x] 验收反查修正：`UIDocumentInspector.cs` 曾夹带绑定表 UI 改动，已收回；当前 diff 只保留 prefab 名称为合法 C# 标识符时不做 Pascal 转换的目标改动。

挂载点核对：
- [x] `UIDocumentGenerator` 输出契约是主挂载点，删除该改动后新 partial 生成能力消失。
- [x] `{Window}.cs` 是用户窗口业务逻辑承载点，删除后业务逻辑没有默认骨架。
- [x] `{Window}.Design.g.cs` 是 `UIOption`、绑定初始化、本地化刷新和释放 helper 挂载点。
- [x] `{Window}.Model` 内嵌类型是强类型组件缓存挂载点。
- [x] `UIDocumentInspector.GetClassName()` 到 generator 的名称输入是 `UI_` 前缀保留的挂载点。
- [x] 拔除沙盘推演：移除 generator/templates/inspector 名称改动和 Loading 迁移后，`UIModule` 仍能打开手写 `UIWindow`，但 UIDocument 绑定代码不会按新 partial 契约生成；旧四件套不会自动恢复。

## 3. 验收场景核对

- [x] N1：`UI_ExampleWindow` 生成 `UI_ExampleWindow.cs`、`UI_ExampleWindow.Design.g.cs`、`UIExampleWindow.Model.g.cs`；由 `Generate_WhenWindowNameContainsUnderscore_PreservesWindowTypeAndCreatesExpectedFiles` 覆盖。
- [x] N2：重复生成不覆盖用户 logic 文件；由 `Generate_WhenLogicFileExists_DoesNotOverwriteUserCode` 覆盖。
- [x] N3：生成源码只保留同一个窗口 partial 类型，不生成 `*Controller` / `*Module`；由生成器测试和 grep 覆盖。
- [x] N4：Button/Text 等组件字段进入内嵌 `Model`，design partial 通过 `Document.GetComponent<T>()` 赋值；由 `Generate_WhenLocalizedTextBindingExists_EmitsRefreshAndSubscription` 覆盖。
- [x] N5：业务逻辑通过 `Bindings.*` 访问组件；生成模板和 Loading 示例均使用 `Bindings`。
- [x] N6：本地化 key 生成刷新和订阅，无 key 时不引用 Localization；由两个 localization 生成器测试覆盖。
- [x] N7：打开窗口后 `OnAwakeAsync()` 先调用设计初始化；由 logic skeleton 编译和测试文本断言覆盖。
- [x] N8：`ReleaseDesign()` 取消本地化订阅并清空 `Bindings`，随后 logic skeleton 调用 `base.Release()`；由模板和测试断言覆盖。
- [x] N9：Loading 示例不再引用 `LoadingController`、`LoadingModule` 或顶层 `LoadingModel`；grep 无命中。
- [x] B1：非法 prefab/class 名转换为合法窗口类型；由 `Generate_WhenClassNameIsNotIdentifier_ConvertsToValidWindowType` 覆盖。
- [x] B2：用户已有 logic 文件不覆盖；由对应测试覆盖。
- [x] B3：重复字段名生成失败；由 `Generate_WhenDuplicateFieldNameExists_Throws` 覆盖。
- [x] B4：本地化组件未被 UI binding 选中时报错；由 `Generate_WhenLocalizedComponentIsNotSelected_Throws` 覆盖。
- [x] E1/E2/E3：未修改 `UIModule` 运行时语义，未继续生成 Module/Controller，未继续生成顶层 model。

验证命令：
- `python .codestable\tools\validate-yaml.py --file .codestable\features\2026-06-22-ui-window-partial-codegen\ui-window-partial-codegen-checklist.yaml --yaml-only`：通过。
- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore -m:1 /p:UseSharedCompilation=false`：通过。
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore -m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false`：通过。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore -m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false`：通过。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore -m:1 /p:UseSharedCompilation=false`：通过。
- `dotnet test GameDeveloperKit.Editor.Tests.csproj --no-restore --no-build --filter UIDocumentGeneratorTests`：命令返回 0。
- `dotnet test GameDeveloperKit.Runtime.Tests.csproj --no-restore --no-build --filter UIModuleTests`：命令返回 0。

验证限制：
- 完整 Editor / Editor.Tests 构建不加 `BuildProjectReferences=false` 时仍会被旧 Unity generated `GameDeveloperKit.StoryPresentation.csproj` 牵连，原因是该 generated csproj 残留已删除 StoryPresentation 源文件引用；这是前序 StoryPlayback 迁移的 Unity 工程生成残留，不属于本 UI feature。
- 本 feature 是 Unity Editor 工具和 C# 生成器改动，不涉及浏览器前端；未做浏览器肉眼验证。

## 4. 术语一致性

- `UI_ExampleWindow`：design、checklist、tests 和 generator 均表示窗口 C# 类型名。
- `UIExampleWindow.Model.g.cs`：表示文件名 stem 去分隔符后的 model partial 文件；验收时已修正 design 中旧写法。
- `Window logic partial`：`{Window}.cs`，用户可编辑，只首次创建。
- `Window design partial`：`{Window}.Design.g.cs`，自动生成，可覆盖，负责 `UIOption`、绑定、本地化和释放 helper。
- `Nested model`：`{Window}.Model` 内嵌类型，不是顶层 `ExampleModel`。
- 禁用词 grep：`LoadingController`、`LoadingModule`、`LoadingModel`、`m_Controller` 在新 UI Custom 示例和生成器输出范围无命中。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已新增 UI 子系统现状，记录 `UIModule`、`UIWindow`、`UIDocument`、`UIDocumentInspector`、`UIDocumentGenerator`、`UIOption` / `UILayer` 的当前职责。
- [x] 架构 doc 已记录生成契约从四件套收敛为 window logic partial + design partial + nested model。
- [x] 架构 doc 已记录 per-window Module / Controller / 顶层 Model 不再生成；业务逻辑写在具体 `UIWindow` partial。
- [x] 架构 doc 已记录 generated design partial 负责 `UIOption`、组件绑定、本地化刷新和释放 helper。
- [x] 架构 doc 已记录 `UIModule` 本体的打开关闭、层级、安全区、资源释放和窗口栈语义不随本 feature 改变。
- [x] `.codestable/attention.md` 候选已在第 8 节登记，不在 acceptance 中直接写入。

## 6. requirement 回写

- [x] `.codestable/requirements/ui-module.md` 已从 `draft` 升级为 `current`。
- [x] `implemented_by` 已加入 `2026-05-27-ui-module`、`2026-05-31-uidocument-inspector-bindings`、`2026-06-19-ui-localization-binding`、`2026-06-22-ui-window-partial-codegen`。
- [x] 用户故事已从旧顶层 `ExampleModel` 口径刷新为 `Bindings` / 窗口内嵌 model 口径，并补充显式文本本地化绑定能力。
- [x] 边界已刷新：UI 模块不负责翻译内容、语言包编辑、缺失 key 管理、多实例同类型窗口、复杂转场、焦点导航或输入系统适配。
- [x] `.codestable/requirements/VISION.md` 已把 `ui-module` 从 Draft 分组移动到 Current 分组，并更新 `last_reviewed`。

## 7. roadmap 回写

- [x] 非 roadmap 起头：design frontmatter 的 `roadmap` / `roadmap_item` 为空，因此不更新 `.codestable/roadmap/*-items.yaml` 或 roadmap 主文档。

## 8. attention.md 候选盘点

- 候选：Unity asmdef/package 迁移后，生成的 `.csproj` 可能残留已删除 asmdef 对应的工程文件；在 Unity Editor 重新生成工程前，完整 `dotnet build` 可能编译旧 generated csproj 并报缺源文件。建议放入 `.codestable/attention.md` 的“命令与脚本陷阱”或“编译与构建”。
- 本 feature 自身没有新增必须每次启动都知道的命令或路径规则。

## 9. 遗留

- 旧业务如果仍手写引用 per-window `Module`、`Controller` 或顶层 `Model`，需要按新契约迁移到 `App.UI.OpenAsync<TWindow>()`、窗口 partial 和 `Bindings`。
- `UIDocumentInspector.cs` 仍偏胖；如果后续继续增加生成选项、命名预览或批量迁移工具，建议单独走 `cs-refactor` 拆分 Inspector。
- design 第 2.5 节建议的长期命名 convention 尚未归档：`UI_` 前缀、`Window` 后缀、`.Design` / `.Model` 文件名规则可考虑走 `cs-decide`。
