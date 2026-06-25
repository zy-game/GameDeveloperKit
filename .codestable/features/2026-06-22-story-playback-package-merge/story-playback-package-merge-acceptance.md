---
doc_type: feature-acceptance
feature: 2026-06-22-story-playback-package-merge
status: passed
accepted_at: 2026-06-22
design: .codestable/features/2026-06-22-story-playback-package-merge/story-playback-package-merge-design.md
roadmap: story-playback-architecture
roadmap_item: story-playback-package-merge
tags: [story, playback, avpro, asmdef]
---

# StoryPlayback Package Merge 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-22
> 关联方案 doc：`.codestable/features/2026-06-22-story-playback-package-merge/story-playback-package-merge-design.md`

## 1. 接口契约核对

- [x] `GameDeveloperKit.StoryPlayback` asmdef 已落地：`Assets/GameDeveloperKit/Runtime/StoryPlayback/GameDeveloperKit.StoryPlayback.asmdef` 引用 `GameDeveloperKit.Runtime`、`AVProVideo.Runtime`、`UniTask`、`UnityEngine.UI` 和 `Unity.TextMeshPro`。
- [x] `StoryMediaCommandNames` 保留在 `Runtime/Story`：常量值仍包含 `play_video`、`show_image`、`play_audio`、`source`、`clip`、`image`、`streaming_assets`、`persistent_data_path`、`network_stream` 和 `completed`。
- [x] 播放协调类型归属 `StoryPlayback`：`StoryPresenter`、`IStoryFramePresenter`、`IStoryCommandHandle`、`IStoryCommandHandler`、`StoryMediaCommandHandler`、视频/图片/音频播放器接口均已在 `Assets/GameDeveloperKit/Runtime/StoryPlayback` 编译。
- [x] 默认播放组件归属 `StoryPlayback`：`StoryPlayerView`、`StoryImageCommandPlayer`、`StorySoundCommandPlayer`、`StoryAvProVideoCommandPlayer`、`StoryVideoPathResolver` 和 `StoryPlayerView.prefab` 已收敛到 `Runtime/StoryPlayback`。
- [x] 删除名词已移除可编译 runtime 类型：`StoryRuntimeDemoBootstrap`、`StoryProcedure`、`StoryProcedureRequest` 不再位于 `Assets/GameDeveloperKit` 源码。

## 2. 行为与决策核对

- [x] `StoryPlayback` 是合并包，不是视频后端接口层：AVPro 播放实现直接位于 `GameDeveloperKit.StoryPlayback`，未保留旧 `GameDeveloperKit.StoryPresentation` / `.AVPro` shim。
- [x] `Runtime/Story` 保持纯剧情核心：grep `Assets/GameDeveloperKit/Runtime/Story` 未命中 `RenderHeads.Media.AVProVideo`、UGUI、TMP、`ResourceModule`、`SoundModule`、`ProcedureBase`、`LoadingModule` 或 `GameDeveloperKit.StoryPlayback`。
- [x] `StoryPlayerView.Play()` / `PlayRegistered()` 仍只启动剧情播放：代码通过 `EnsurePresenter().Start()` / `StartProgram()` 创建并呈现 `StoryFrame`，不调用 `App.Startup()`、Procedure 切换或 Loading UI。
- [x] 媒体命令行为保持：视频仍走 `StoryAvProVideoCommandPlayer` + `StoryVideoPathResolver` + AVPro `OpenMedia(MediaPathType.AbsolutePathOrURL, resolvedPath, true)`；图片仍走 `ResourceModule` + `RawImage`；音频仍走 `SoundModule`。
- [x] 视频三来源规则保持：`StoryVideoPathResolver` 支持 `streaming_assets`、`persistent_data_path`、`network_stream`，拒绝 `guid:`、越界相对路径、错误本地绝对路径和来源不匹配路径。
- [x] 明确不做已守住：`Assets/GameDeveloperKit` grep 未命中 `StoryAvProVideoPreloadQueue`、`StoryAvProVideoPreloadStatus`、`StartupProcedure`、`StoryTestProcedure` 或 `UnityEngine.Video.VideoPlayer`。

挂载点核对：
- [x] 新增挂载点：`Runtime/StoryPlayback/GameDeveloperKit.StoryPlayback.asmdef`。
- [x] Editor/Test asmdef：`GameDeveloperKit.Editor`、`GameDeveloperKit.Editor.Tests`、`GameDeveloperKit.Runtime.Tests` 均引用 `GameDeveloperKit.StoryPlayback`。
- [x] Prefab builder：`StoryPlayerViewPrefabBuilder` 的 prefab 路径已改为 `Assets/GameDeveloperKit/Runtime/StoryPlayback/StoryPlayerView.prefab`。
- [x] 拔除沙盘推演：删除旧 `Runtime/StoryPresentation*` 后，源码和 asmdef 中无旧程序集名、旧 bootstrap/procedure 类型或旧 prefab 路径残留；后续若撤销本 feature，主要回退点就是新 asmdef、迁入文件、asmdef 引用和 prefab builder 路径。

## 3. 验收场景核对

- [x] N1 新播放程序集：`GameDeveloperKit.StoryPlayback.asmdef` 存在；旧 `GameDeveloperKit.StoryPresentation` / `.AVPro` asmdef 已删除。
- [x] N2 纯 Story 核心：Story runtime 依赖边界 grep 通过，无播放/UI/Procedure 反向依赖。
- [x] N3 命令协议保留：`StoryMediaCommandNames` 位于 `GameDeveloperKit.Runtime`，Editor compiler、schema、validation 和 tests 继续引用该类型。
- [x] N4 播放 API 保持：`StoryPlayerView.Play()` / `PlayRegistered()` 调用 `StoryPresenter.Start()` / `StartProgram()` 并渲染 frame。
- [x] N5 视频播放保持：`StoryAvProVideoCommandPlayer` 解析 `source + clip` 后创建 `StoryAvProVideoPlayback`，首帧事件触发 `StoryPlayerView` 刷新视频输出。
- [x] N6 图片/音频保持：`StoryImageCommandPlayer` 使用 `ResourceModule.LoadAssetAsync()` 输出 `Texture` / `Sprite.texture` 到 `RawImage`；`StorySoundCommandPlayer` 使用 `SoundModule.PlayMusicAsync()` / `PlaySfxAsync()`。
- [x] N7 删除旧引导：旧 bootstrap/procedure/request 类型 grep 无命中。
- [x] N8 旧程序集清理：`Assets/GameDeveloperKit` 中旧 presentation 程序集名 grep 无命中。
- [x] N9 Prefab 路径：builder 路径指向 `Runtime/StoryPlayback/StoryPlayerView.prefab`。
- [x] E1/E2/E3 范围守护：未新增预热队列、通用 Startup Procedure、Scripts StoryTest Procedure 或 Unity 内置 VideoPlayer 后端。

验证命令：
- `rg -n "GameDeveloperKit\.StoryPresentation|StoryPresentation\.AVPro|StoryRuntimeDemoBootstrap|StoryProcedureRequest|StoryProcedure\b" Assets\GameDeveloperKit -g "!*.meta"`：无命中。
- `rg -n "RenderHeads\.Media\.AVProVideo|UnityEngine\.UI|TMPro|ResourceModule|SoundModule|ProcedureBase|LoadingModule|GameDeveloperKit\.StoryPlayback" Assets\GameDeveloperKit\Runtime\Story -g "*.cs"`：无命中。
- `rg -n "StoryAvProVideoPreloadQueue|StoryAvProVideoPreloadStatus|UnityEngine\.Video\.VideoPlayer|StartupProcedure|StoryTestProcedure" Assets\GameDeveloperKit -g "!*.meta"`：无命中。
- `dotnet build GameDeveloperKit.StoryPlayback.csproj --no-restore -m:1 /p:UseSharedCompilation=false`：通过。
- `dotnet build GameDeveloperKit.Runtime.csproj --no-restore -m:1 /p:UseSharedCompilation=false`：通过。
- `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore -m:1 /p:UseSharedCompilation=false`：通过。
- `dotnet build GameDeveloperKit.Editor.csproj --no-restore -m:1 /p:UseSharedCompilation=false`：未通过，原因是 Unity 生成的旧 `GameDeveloperKit.StoryPresentation.csproj` 仍显式包含已删除的 `Runtime/StoryPresentation/StoryImageCommandPlayer.cs` 和 `StorySoundCommandPlayer.cs`。当前 `GameDeveloperKit.Editor.csproj` 本身已引用 `GameDeveloperKit.StoryPlayback.csproj`，需由 Unity 重新生成工程清理旧 csproj。
- `dotnet build GameDeveloperKit.Editor.Tests.csproj --no-restore -m:1 /p:UseSharedCompilation=false`：同上，因依赖 Editor 工程时触发旧 generated csproj 失败。

## 4. 术语一致性

- `Runtime/Story`：代码中只保留剧情推进、frame、command data protocol 和 validation；未出现 `StoryPlayback` 反向依赖。
- `StoryPlayback`：目录和 asmdef 使用 `GameDeveloperKit.StoryPlayback`；C# namespace 暂沿用 `GameDeveloperKit.Story`，符合 design 中“减少源代码调用点 churn”的决策。
- `StoryMediaCommandNames`：作为命令数据协议保留在 `Runtime/Story`，不是播放实现。
- 防冲突：`Assets/GameDeveloperKit` grep 无旧 `GameDeveloperKit.StoryPresentation` / `StoryPresentation.AVPro` 可编译引用；历史 design/roadmap 文档中的旧名只作为迁移背景保留。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 `GameDeveloperKit.StoryPlayback` 取代旧 `StoryPresentation` / `StoryPresentation.AVPro`，并列出 `StoryPresenter`、`StoryMediaCommandHandler`、三类媒体播放器、`StoryVideoPathResolver` 和 `StoryPlayerView`。
- [x] 架构边界已更新：`Runtime/Story` 只保存剧情核心与命令数据协议；默认播放由 `GameDeveloperKit.StoryPlayback` 承接。
- [x] 约束已更新：AVProVideo 只允许存在于 `GameDeveloperKit.StoryPlayback` 和 Editor playback；Startup、Loading、Procedure 切换和章节预加载不属于 `StoryPlayback`。
- [x] 历史 changelog 中 2026-06-21 的旧 AVPro 边界已标明为“当时状态”，避免和 2026-06-22 当前边界冲突。

## 6. requirement 回写

- [x] `.codestable/requirements/story-module.md` 已加入 `2026-06-22-story-playback-package-merge` 到 `implemented_by`。
- [x] requirement 边界已补充：默认播放能力由独立 `StoryPlayback` 播放包承接；剧情核心本身仍不持有 UGUI、AVPro、Sound、Resource 或 Procedure。
- [x] 实现进展已补充：默认播放层合并为 `GameDeveloperKit.StoryPlayback`，旧 StoryPresentation 引导层删除。
- requirement 保持 `draft`：本 feature 只完成播放包合并，完整 story-module 能力仍缺 Startup、剧情测试入口、预热和后续剧情系统收敛项。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-playback-architecture/story-playback-architecture-items.yaml` 已把 `story-playback-package-merge` 从 `in-progress` 改为 `done`，feature 仍指向 `2026-06-22-story-playback-package-merge`。
- [x] `.codestable/roadmap/story-playback-architecture/story-playback-architecture-roadmap.md` 第 5 节子 feature 清单已同步为 `状态：done` / `对应 feature：2026-06-22-story-playback-package-merge`。
- [x] roadmap 观察项已更新为架构边界已回写。
- [x] YAML 校验通过：`python .codestable\tools\validate-yaml.py --file .codestable\roadmap\story-playback-architecture\story-playback-architecture-items.yaml --yaml-only`。

## 8. attention.md 候选盘点

- 候选：Unity asmdef/package 迁移后，生成的 `.csproj` 可能仍残留已删除 asmdef 对应的工程文件；在 Unity Editor 重新生成工程前，Editor/Test 的 `dotnet build` 可能会编译旧 generated csproj 并报缺源文件。建议放入 `.codestable/attention.md` 的“命令与脚本陷阱”或“编译与构建”。
- 已有注意事项仍适用：同一项目 Unity Editor 已打开时，不要在 batchmode 跑 Unity Test Runner；需要在现有 Editor 内跑或先关闭该项目 Editor。

## 9. 遗留

- `story-playback-video-prewarm`：视频队列缓冲/预热仍是 roadmap 后续项，本 feature 没实现。
- `startup-procedure-entry`：通用 Startup Procedure 入口仍是 roadmap 后续项。
- `story-test-scripts-entry`：`Assets/GameDeveloperKit/Scripts` 下剧情测试 asmdef/入口仍是 roadmap 后续项。
- Unity 生成 csproj 局限：`GameDeveloperKit.Editor.csproj`、`GameDeveloperKit.Editor.Tests.csproj` 当前已指向 `GameDeveloperKit.StoryPlayback.csproj`，但旧 `GameDeveloperKit.StoryPresentation.csproj` / `.AVPro.csproj` 仍残留并包含已删除源文件；需 Unity 重新生成工程清理，本次不手写 generated csproj。
