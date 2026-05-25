# download-module 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-05-20
> 关联方案 doc：`.codestable/features/2026-05-20-download-module/download-module-design.md`

## 1. 接口契约核对

**接口示例逐项核对**：
- [x] 示例 `Super.Download.DownloadAsync("https://example.com/a.bin")` → `DownloadHandler`：`Super.Download` 已在 `Assets/GameDeveloperKit/Runtime/Super.cs:14` 暴露；`DownloadModule.DownloadAsync` 在 `DownloadModule.cs:27` 返回 `DownloadHandler`；`DownloadHandler.WaitCompletionAsync` 在 `DownloadHandler.cs:48` 暴露，行为一致。

**名词层"现状 → 变化"逐项核对**：
- [x] `DownloadModule`：`DownloadModule.cs:9` 实现 `IGameModule`；`Startup/Shutdown/Release`、`DownloadAsync`、`DownloadListAsync`、`Pause/Resume/Cancel/CancelAll/HasDownload/GetDownload` 均存在。
- [x] `DownloadStatus`：`DownloadStatus.cs:3` 定义 `None/Waiting/Downloading/Paused/Completed/Failed/Canceled`。
- [x] `DownloadFailureKind`：`DownloadFailureKind.cs:3` 定义 `None/Network/Timeout/HttpStatus/FileIO/InvalidResponse/Canceled`。
- [x] `DownloadHandler`：`DownloadHandler.cs:9` 定义单文件控制柄；状态/进度/字节数/temp 路径/失败类型/分片计数/事件/等待/暂停恢复取消均落地。
- [x] `DownloadChunk`：`DownloadChunk.cs:3` 为内部类型，记录分片 index/range/part path/status。
- [x] `DownloadListHandler`：`DownloadListHandler.cs:7` 定义批量控制柄；`Items/Status/Progress/WaitCompletionAsync/Pause/Resume/Cancel` 与事件均落地。

**流程图核对**：
- [x] `Caller -> DownloadModule -> DownloadHandler`：`DownloadModule.DownloadAsync` 创建/复用 handler 并启动。
- [x] temp 检查与 HTTP 请求：`DownloadHandler.DownloadSingleStreamAsync` / `DownloadChunkedAsync` 中检查 temp/part 并发起 `UnityWebRequest`。
- [x] completed/paused/canceled/failed 分支：`DownloadHandler` 状态机和事件均有实际落点。

## 2. 行为与决策核对

**需求摘要逐项验证**：
- [x] 单文件下载：`DownloadModule.DownloadAsync` + `DownloadHandler` 已落地，支持状态、进度、错误、temp 路径、暂停/恢复/取消、回调、`WaitCompletionAsync`。
- [x] 批量下载：`DownloadListHandler` 聚合多个 handler，顺序执行，单项失败不阻断后续项。
- [x] 下载期间只写 temp：`DownloadModule.Startup` 固定 temp 根目录为 `Application.temporaryCachePath/downloads`。

**明确不做逐项核对**：
- [x] 无资源版本比对、清单解析、差分补丁、解压、业务 hash。
- [x] 无自动写入 `FileModule`；grep `Assets/GameDeveloperKit/Runtime/Download` 未命中 `FileModule`。
- [x] 无动态分片调优、分片重试退避配置、跨进程恢复。
- [x] 无下载队列优先级、限速、全局并发数公开配置。
- [x] 无认证、Cookie、自定义证书策略。

**关键决策落地**：
- [x] temp 落盘：`DownloadModule.cs:16` 使用 `Application.temporaryCachePath/downloads`。
- [x] Range 续传：`DownloadHandler.cs:180` 设置 `Range: bytes={existingLength}-`；206 追加，非 206 回退重下。
- [x] 大文件分片：`DownloadHandler.cs:118` 根据 `LargeFileThreshold` 和 Range 支持切分；`DownloadHandler.cs:276` 对每片设置 Range。
- [x] 暂停/失败/取消语义：暂停保留，失败保留，取消删除 temp/part。
- [x] URL 注册表：`DownloadModule` 用 `m_Downloads` 维护 url → handler；重复 URL 复用。
- [x] 不暴露底层 Task：公开等待接口是 `WaitCompletionAsync()`。

**流程级约束核对**：
- [x] 参数非法抛 `ArgumentNullException` / `ArgumentException`。
- [x] 网络/HTTP/IO/InvalidResponse 进入 `Failed` 并记录 `Error` / `FailureKind`。
- [x] `Pause/Cancel/Resume` 幂等约束落地；模块级 URL 控制找不到任务返回 completed task。
- [x] `Failed` 不清理 temp/part；`Cancel/CancelAll` 清理。
- [x] 未知总大小时 `TotalBytes=-1` 可继续下载。
- [x] 分片完整性以 part 长度匹配判断，合并前逐片检查。

**挂载点反向核对**：
- [x] M1 `Super.Download`：`Super.cs:14`。
- [x] M2 `Assets/GameDeveloperKit/Runtime/Download/`：下载模块实现集中在该目录。
- [x] M3 temp 下载目录：`DownloadModule.cs:16`。
- [x] 反向核查：grep `DownloadModule|DownloadHandler|DownloadListHandler|DownloadStatus|DownloadFailureKind|DownloadChunk|Super.Download`，挂载点均在清单内；`GameDeveloperKit.Runtime.csproj` 为 Unity/IDE 编译项更新。
- [x] 拔除沙盘推演：删除 `Runtime/Download/`、移除 `Super.Download`、删除 csproj Download compile 项即可拔除功能；temp 目录运行时生成，无代码残留。

## 3. 验收场景核对

- [x] N1 单文件下载成功：`DownloadHandler` 状态完成、temp 文件路径和字节数逻辑已实现；证据：代码 + 编译。
- [x] N2 完成回调：`Completed?.Invoke(this)` 在完成路径触发；证据：代码。
- [x] N3 UniTask 等待：`WaitCompletionAsync()` 返回 completion source task；证据：代码。
- [x] N4 进度观察：`ProgressChanged` 在下载/分片/完成路径触发；证据：代码。
- [x] N5 暂停：`Pause()` 设置 `Paused`，请求 loop abort，不删除文件；证据：代码。
- [x] N6 恢复：`Resume()` 支持 `Paused/Failed`，重置 completion 并重新运行；证据：代码。
- [x] N7 取消：`Cancel()` 设置 `Canceled` 并删除 temp/part；证据：代码。
- [x] N8 批量成功：`DownloadListHandler` 顺序等待所有 item 后 Completed；证据：代码。
- [x] N9 批量失败继续：子 item failed 仍完成 `WaitCompletionAsync`，列表继续下一项；证据：代码。
- [x] N10 URL 查询：`HasDownload/GetDownload` 基于注册表返回；证据：代码。
- [x] N11 重复下载同 URL：`GetOrCreateHandler` 复用已有 handler；证据：代码。
- [x] N12 全部取消：`CancelAll()` 遍历所有 handler 并清空注册表；证据：代码。
- [x] N13 大文件分片：达到阈值且 Range 支持时 `IsChunked=true`，下载 part 并合并；证据：代码。
- [x] N14 分片暂停恢复：已完成 part 由 `IsChunkComplete` 跳过；证据：代码。
- [x] N15 分片取消：`DeleteTempFiles` 删除所有 part；证据：代码。
- [x] N16 普通失败恢复：Failed 保留 temp，Resume 后按 temp 长度续传或重下；证据：代码。
- [x] N17 分片失败恢复：Failed 保留 part，Resume 后跳过已完成 part；证据：代码。
- [x] B1 Range 不支持：append 请求非 206 时删除 temp 并单流重下；证据：代码。
- [x] B2 未知总大小：`TotalBytes=-1` 时仍走单流下载并完成后回填已下载大小；证据：代码。
- [x] B3 非法 URL：`ValidateUrl` 抛 `ArgumentNullException` / `ArgumentException`；证据：代码。
- [x] B4 大文件但不支持 Range：`useChunked = supportsRange && total >= threshold`，不支持 Range 回退单流；证据：代码。
- [x] E3 分片长度不匹配：`IsChunkComplete` 不满足时 Failed；证据：代码。
- [x] E1 网络错误：`CreateDownloadException` 分类 Network/HttpStatus/InvalidResponse，Failed 记录 Error；证据：代码。
- [x] E2 反向核对：grep 下载目录无 `FileModule`、Compression、Cookie、Certificate、diff、patch 命中。

## 4. 术语一致性

- `DownloadModule`：代码命中与方案一致。
- `DownloadHandler`：代码命中与方案一致；Unity 同名类型通过别名 `UnityDownloadHandlerFile` / `UnityDownloadHandlerBuffer` 避免冲突。
- `DownloadListHandler`：代码命中与方案一致。
- `临时文件`：代码使用 `TempPath` / temp root 表达，与方案一致。
- `断点续传`：代码使用 Range 与 temp 长度，和方案一致。
- `分片下载`：代码使用 `DownloadChunk` / `.part` / Range，和方案一致。
- `失败恢复`：代码从 `Failed` 的 `Resume()` 继续，和方案一致。

## 5. 架构归并

- [x] `.codestable/architecture/ARCHITECTURE.md`：已新增 Download 子系统，记录入口、核心类型、存储策略和关键行为。
- [x] `.codestable/architecture/ARCHITECTURE.md`：已在约束节补充下载临时落盘、恢复语义、批量失败继续。

## 6. requirement 回写

- [x] design frontmatter 未指定 `requirement`，方案第 4 节明确本 feature 不新增 requirement 文档；本次跳过 requirement 回写。

## 7. roadmap 回写

- [x] design frontmatter 无 `roadmap` / `roadmap_item`，非 roadmap 起头；跳过 roadmap 回写。

## 8. attention.md 候选盘点

- [x] 候选：`dotnet build GameDeveloperKit.Runtime.csproj --no-restore` 可用于 Runtime 模块快速编译验证；是否需要写入 attention.md 由用户决定。

## 9. 遗留

- 已知限制：未做真实 HTTP 本地服务集成测试；当前证据为代码核对、IDE diagnostics、Runtime csproj 编译通过。
- 已知限制：下载队列优先级、限速、分片重试退避、跨进程分片任务恢复均按方案不做。
- 顺手发现：`UserSettings/Layouts/default-2022.dwlt` 有既有未相关修改，本 feature 未触碰。
