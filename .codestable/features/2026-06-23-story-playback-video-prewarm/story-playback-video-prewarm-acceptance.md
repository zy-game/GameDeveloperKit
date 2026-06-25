# story-playback-video-prewarm 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-06-23
> 关联方案 doc：.codestable/features/2026-06-23-story-playback-video-prewarm/story-playback-video-prewarm-design.md

## 1. 接口契约核对

**接口示例逐项核对**

- [x] `StoryAvProVideoPreloadStatus`：包含 `Pending`、`ReadyToPlay`、`FirstFrameReady`、`Failed`、`Canceled`，与方案一致。
- [x] `StoryAvProVideoPreloadHandle`：暴露 `Command`、`Source`、`ClipPath`、`ResolvedPath`、`Status`、`Error`、`IsTerminal`、`CanAcquire` 和 `StatusChanged`。
- [x] `StoryAvProVideoPreloadQueue`：暴露 `Capacity`、`Count`、`PreloadAsync`、`TryAcquire`、`Release`、`Clear` 和 `Dispose`。
- [x] `StoryAvProVideoCommandPlayer`：暴露 `PreloadQueue`、`PreloadLookAheadCount`、`PreloadVideoAsync`，并保留 `PlayVideo()`、`ActivePlaybacks`、`PlaybackStarted`。

**名词层变化核对**

- [x] `StoryAvProVideoPlayback` 已拆到独立文件，支持 `CreatePreloaded()`、`Preload()` 和 `AttachHandle()` 两阶段生命周期。
- [x] 预热队列按 `source + clipPath` 建 key；`PreloadResolvedAsync()` 与 `PlayVideo()` 共用 `TryResolveMediaPath()` / `StoryVideoPathResolver` 口径。
- [x] `StoryPlayerView.EnsurePresenter()` 默认创建容量为 2 的预热队列，并设置 look-ahead 为 1。

**流程图核对**

- [x] `PreloadAsync -> Resolve -> CreatePreloaded -> Preload -> ReadyToPlay/FirstFrameReady` 均有代码落点。
- [x] `PlayVideo -> TryAcquire -> AttachHandle + Play` 命中路径已落地；未命中走原即时创建 `StoryAvProVideoPlayback`。
- [x] `PlayVideo -> PreloadNextVideos` 的 best-effort 前瞻已落地，异常不会反向影响当前播放命令。

## 2. 行为与决策核对

**需求摘要逐项验证**

- [x] 预热名词已存在，并位于 `GameDeveloperKit.StoryPlayback`。
- [x] 三种视频来源仍由 `StoryVideoPathResolver` 解析，视频不回到资源包。
- [x] AVPro 预热通过 `OpenMedia(..., autoPlay:false)` 进入准备态；`ReadyToPlay` 后静音 `Play()`，`FirstFrameReady` 后 `Pause()`。
- [x] `PlayVideo()` 优先 Acquire；未命中或 pending 时释放旧条目并 fallback 即时播放。
- [x] Acquire 后播放实例进入 `ActivePlaybacks`，由 `StoryAvProVideoPlayback` 负责 command handle 完成、失败、取消、停止和释放。

**明确不做逐项核对**

- [x] 未恢复 `StoryRuntimeDemoBootstrap`、`StoryProcedure`、`StoryProcedureRequest` 或旧章节媒体预加载。
- [x] 预热文件未调用 `LoadingWindow`、`LoadingModule`、`App.Startup`、`App.Procedure`、`ChangeAsync`。
- [x] 预热视频不调用 `ResourceModule`、`LoadAssetAsync`、`AssetBundle` 或 `Resources`。
- [x] 未新增 `UnityEngine.Video.VideoPlayer` 或可替换后端。
- [x] `Runtime/Story` 未新增 AVProVideo、UGUI、Application 路径 API 或 StoryPlayback 依赖。

**关键决策落地**

- [x] 预热队列放在 StoryPlayback，不进入 Story 核心。
- [x] 队列 key 使用 `source + clipPath`，播放和预热路径解析口径一致。
- [x] `StoryAvProVideoPlayback` 支持准备播放器与绑定 command handle 两阶段。
- [x] FirstFrame 预热采用静音播放到首帧后暂停。
- [x] 默认前瞻只顺序扫描当前章节后续少量 `play_video`，不做分支预测。

**流程级约束核对**

- [x] 错误语义：预热失败只写 `StoryAvProVideoPreloadHandle.Failed/Error`，不自动 fail Story command。
- [x] 幂等性：队列已有同 key 时返回现有 handle 等待结果，不创建第二个条目。
- [x] 容量：容量满时只从队列字典里淘汰未 Acquire 条目；`ActivePlaybacks` 不在队列容量逻辑里。
- [x] 取消：取消 token 只通过 `CancelEntry()` 释放未 Acquire 条目；Acquire 后 token 不再关联 active playback。
- [x] 顺序：`TryAcquire()` 只接受 `CanAcquire`，pending 预热不阻塞 `PlayVideo()`。
- [x] 生命周期：`StoryAvProVideoCommandPlayer.Dispose()` 同时释放 active playbacks 和 `PreloadQueue`。
- [x] 可观测：handle 暴露 source、clip、resolved path、status、error；`PlaybackStarted` 仍表示 active playback 可显示。

**挂载点反向核对**

- [x] `StoryAvProVideoCommandPlayer.PreloadQueue` 是默认 AVPro 播放器的预热入口。
- [x] `StoryAvProVideoCommandPlayer.PreloadLookAheadCount` 控制自动前瞻预热数量。
- [x] `StoryAvProVideoPreloadQueue` / `Handle` / `Status` 是手动预热和状态观察契约。
- [x] `StoryPlayerView` 只配置预热队列与 look-ahead，不接入 LoadingWindow 或 UIModule 业务逻辑。
- [x] 拔除沙盘：删除 preload queue/handle/status 与 command player 上的 `PreloadQueue` / `PreloadLookAheadCount` / `PreloadVideoAsync` 后，StoryPlayback 退回即时 AVPro 播放；Story 核心、StoryTest 和 Startup 边界不受影响。

## 3. 验收场景核对

- [x] **N1-N3 三种来源路径一致**
  - 证据来源：`StoryVideoPathResolverTests` 覆盖 streaming assets、persistent data path、network stream；`PreloadVideoAsync()` 和 `PlayVideo()` 共用同一解析口径。
  - 结果：通过。
- [x] **N4-N5 重复预热与容量淘汰**
  - 证据来源：代码审阅 `m_Entries.TryGetValue(key)`、`EvictIfNeeded()`；编译通过。
  - 结果：通过。未引入真实 AVPro 媒体文件的 Unity PlayMode 计数测试，保留为后续手测增强点。
- [x] **N6-N7 命中预热复用，未命中即时播放**
  - 证据来源：代码审阅 `PlayVideo()` acquire-first 分支和 fallback 分支；编译通过。
  - 结果：通过。
- [x] **N8-N9 失败和取消只影响 preload handle**
  - 证据来源：`StoryVideoPreloadTests.PreloadVideoAsync_WhenPathInvalid_ReturnsFailedHandle`、`PreloadQueue_WhenPathInvalid_ReturnsFailedHandleWithoutQueueEntry` 与取消路径代码审阅。
  - 结果：通过。
- [x] **N10-N11 前瞻预热与 RawImage 首帧**
  - 证据来源：`StoryPlayerView_WhenPresenterCreated_ConfiguresVideoPreloadQueue` 验证默认队列和 look-ahead；`StoryPlayerView.UpdateVideoOutput()` 继续从 active playback 读取 `CurrentTexture`。
  - 结果：通过。真实 AVPro 首帧显示依赖 Unity 运行时和实际视频文件，需在 Editor/设备手测确认视觉空白期改善。
- [x] **B1-B4 范围守护**
  - 证据来源：grep 无命中和程序集编译通过。
  - 结果：通过。

## 4. 术语一致性

- `StoryAvProVideoPreloadQueue`、`StoryAvProVideoPreloadHandle`、`StoryAvProVideoPreloadStatus` 与 design、roadmap、代码一致。
- `ReadyToPlay` / `FirstFrameReady` 与 AVPro event 名称一致。
- `source + clipPath`、`Acquire`、`PreloadLookAheadCount`、`StoryPlayback` 术语一致。
- 禁用词 grep：旧 `StoryRuntimeDemoBootstrap` / `StoryProcedureRequest` / `StoryRuntimeChapterPreload` 未回流到新预热实现。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md` 已补充 `StoryAvProVideoPreloadQueue` / `Handle` / `Status` 名词。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已记录 `PlayVideo()` acquire-first、未命中 fallback 和 `StoryPlayerView` 通过 active playback 纹理刷新 `RawImage`。
- [x] `.codestable/architecture/ARCHITECTURE.md` 已补充 StoryPlayback AVPro 预热边界：不做章节资源预加载、Loading UI、Procedure 切换、ResourceModule 视频加载或 Story 核心依赖。

## 6. requirement 回写

- [x] `.codestable/requirements/story-module.md` 已追加 `2026-06-23-story-playback-video-prewarm` 到 `implemented_by`。
- [x] `.codestable/requirements/story-module.md` 已记录 StoryPlayback 支持 AVPro 视频队列预热、默认小容量队列和 look-ahead。
- [x] `story-module` 已从 `draft` 升为 `current`，因为本 roadmap 中阻塞它 current 的 StoryPlayback 播放包、测试入口和视频预热已落地。
- [x] `.codestable/requirements/VISION.md` 已把 `story-module` 从 Draft 移到 Current。

## 7. roadmap 回写

- [x] `.codestable/roadmap/story-playback-architecture/story-playback-architecture-items.yaml` 中 `story-playback-video-prewarm` 已从 `in-progress` 改为 `done`。
- [x] `.codestable/roadmap/story-playback-architecture/story-playback-architecture-roadmap.md` 子 feature 清单已同步为 `done`，并记录预热实现内容。
- [x] roadmap 变更日志已追加 2026-06-23 完成记录。

## 8. attention.md 候选盘点

- [x] 候选：不要并行运行会写同一 Unity `obj/Debug/*.dll` 的 `dotnet build`，可能出现 CS2012 文件锁；需要顺序运行相关 csproj。建议后续用 `cs-note` 加到 attention.md。

## 9. 遗留

- 后续优化点：补一条真实 Unity PlayMode 或手工场景验证，使用实际视频文件观察切换空白期是否缩短，并覆盖命中 FirstFrameReady 后 RawImage 同帧显示。
- 已知限制：前瞻预热只做当前章节顺序扫描，不预测分支、不跨章节；这是 design 明确边界。
- 实现阶段顺手发现：`StoryPlayerView.cs` 仍偏胖，后续若继续扩 UI 能力建议单独走 `cs-refactor` 拆默认 UI builder / runtime renderer。
- 验证命令：
  - `dotnet build GameDeveloperKit.StoryPlayback.csproj --no-restore`
  - `dotnet build GameDeveloperKit.Runtime.Tests.csproj --no-restore`
