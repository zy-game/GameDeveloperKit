---
doc_type: requirement
slug: runtime-diagnostics
pitch: 提供统一运行时调试入口，让日志、分析表、性能指标和命令工具集中可看、可执行。
status: draft
last_reviewed: 2026-06-19
implemented_by: [2026-06-18-runtime-module-profile-handles, 2026-06-19-network-debug-log-transport-contract]
tags: [debug, analytics, runtime, command]
---

# 运行时诊断工具集

## 用户故事

- 作为测试或 QA，我希望在游戏运行时直接看到日志、帧率、内存和设备指标，而不是每次都连 Editor 或翻系统日志。
- 作为线上问题排查者，我希望测试包能实时把关键日志送到服务端，而不是依赖本地文件或手动上传包。
- 作为业务开发者，我希望埋点和调试命令有统一入口，而不是每个系统自己写一套临时按钮、作弊码或上报代码。
- 作为框架维护者，我希望框架只有一个清晰的运行时调试入口，而不是 Logger、Debug、Diagnostics 几套叫法并存。

## 为什么需要

当前调试能力还停留在基础日志和零散工具阶段，不能覆盖真实运行时排查：看不到完整最近日志列表，各模块缺少统一分析表，性能和命令工具也没有统一入口。随着模块增多，问题会分散在资源、下载、配置、UI 和业务链路里，需要用 Debug 作为唯一入口，把这些线索收束起来。

## 怎么解决

提供一个运行时调试能力：系统持续收集最近日志和关键指标，用 IMGUI 按需展示全屏调试面板，允许模块注册自己的 ProfileHandle 分析表，并让命令模块支持通过命令名和参数执行调试命令。日志后续接服务端时走实时 API transport，不依赖本地存储或上传包。

## 边界

- 它不新增独立 DiagnosticsModule，也不保留独立 Logger 入口；日志只是 Debug 调试入口下的一项能力。
- 它不承诺首版接入某个固定云平台、崩溃 SDK 或商业 analytics 服务。
- 它不做本地日志持久化、离线日志包或手动上传包。
- 它不使用 UGUI 或业务 UI 模块绘制调试界面；运行时面板只使用 IMGUI。
- 它不在调试模块里另建 GM 命令系统；命令名和参数执行能力属于 CommandModule。
- 它不默认收集隐私数据、账号凭证、支付信息或业务敏感字段。
- 它不作为玩家正式 UI；运行时面板默认面向开发、QA、测试包或明确开启的诊断场景。

## 变更日志

- 2026-06-18：`2026-06-18-runtime-module-profile-handles` 已实现 Timer / Procedure / Combat 自绘 ProfileHandle 与 Debug Profiles tab 软接入；本 requirement 仍保持 draft，因为实时网络日志 bridge 等能力尚未完成。
- 2026-06-19：`2026-06-19-network-debug-log-transport-contract` 已实现 Debug/Logger 侧 `DebugLogPayload` / `IDebugLogNetworkSender` / `DebugLogNetworkBridge`，Debug 已脱敏日志可以通过显式 flush 交给外部发送实现；本 requirement 仍保持 draft，因为命令工具和更完整网络实时发送能力仍不是统一闭环。
