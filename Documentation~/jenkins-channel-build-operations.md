---
doc_type: dev-guide
slug: jenkins-channel-build-operations
component: channel-build-pipeline
status: current
summary: 配置和运行Jenkins渠道构建、资源staging、promote与rollback
tags: [jenkins, channel-build, resource-release, operations]
last_reviewed: 2026-07-19
---

# Jenkins Channel Build Operations

## 概述

仓库提供两个 Declarative Pipeline：`Jenkinsfile` 构建渠道 Player，并可选择把资源上传为 staged release、审批后 promote；`Jenkinsfile.rollback` 把 current pointer 切换到一个已经验证存在的历史 staged release。Unity 只产出本地构建和 report，Jenkins 管理凭据、审批、归档与恢复。

Jenkins 适配已在本地安装的 Jenkins LTS 和真实 Unity batchmode job 中验证。COS stage、promote 与 rollback 仍必须在隔离 job 和测试 bucket 完成本文的环境验收清单。

## 前置依赖

Windows agent 使用 label `unity-windows`，需要：

- `UNITY_EDITOR_PATH` 指向 Unity Editor executable；版本应与项目实际锁定版本一致。
- Android Player build 需要同版本 Android Build Support，包括 Android SDK、Command-line Tools、NDK 与 OpenJDK；Unity External Tools 必须启用 embedded SDK/NDK/JDK。
- `GDK_UNITY_FIXTURE_ROOT` 指向agent本地的短绝对路径；Windows必须配置成接近盘符根的目录，避免SBP临时bundle路径超过MAX_PATH。
- `pwsh`、`dotnet` 与 `git` 可从 agent PATH 调用。
- agent service account 可创建`GDK_UNITY_FIXTURE_ROOT`和workspace下的`Build/Channel`；未配置时才回退`$WORKSPACE_TMP`或`$WORKSPACE@tmp`。
- Jenkins 安装 Declarative Pipeline、Git、Credentials Binding、JUnit、PowerShell 和 Timestamper 插件；Artifact Archiver 使用 Jenkins core，本 pipeline 不要求 Lockable Resources。
- 同一 job 不并发；Jenkinsfile 已配置 `disableConcurrentBuilds()`。

创建两个 Pipeline job，都从本仓库 SCM 读取脚本：

| Job | Script Path | 用途 |
|---|---|---|
| Channel build | `Jenkinsfile` | quality、validate/player build、stage、可选 promote |
| Resource rollback | `Jenkinsfile.rollback` | 审批后切到已存在的历史版本 |

## Job 环境与凭据

在 Jenkins folder/job 配置非敏感环境变量；值由运维环境决定，不写入仓库：

| 变量 | 要求 |
|---|---|
| `GDK_UNITY_FIXTURE_ROOT` | Windows agent本地短绝对路径，例如`D:\gdk-ci`；pipeline仅使用其`q`和`s`子目录，同一job不并发并允许清理这些子目录 |
| `GDK_COS_REGION` | COS region，例如测试环境对应 region id |
| `GDK_COS_BUCKET` | 已存在的目标 bucket 名；pipeline 不创建或删除 bucket |
| `GDK_COS_CREDENTIAL_ID` | Jenkins username/password credential id；username映射SecretId，password映射SecretKey |
| `GDK_RESOURCE_SIGNING_CREDENTIAL_ID` | Jenkins secret file credential id；文件内容为当前受信RSA private PEM |
| `GDK_RESOURCE_SIGNING_KEY_ID` | 与客户端信任配置一致的非敏感签名key id |

Credential 作用域固定如下：

- build/report/archive 不取得任何 COS 或 RSA credential。
- `Publish Immutable Resources` 只取得 COS username/password，不取得 RSA file。
- `Promote Resource Pointer` 与 rollback 只在审批通过后取得 COS 与 RSA file credential。
- 不归档 credential file、环境 dump、命令行 secret、pointer private material；归档结果只含非敏感对象 key/hash/count/ETag。

## 运行渠道构建

主 job 参数：

| 参数 | 说明 |
|---|---|
| `CHANNEL` | 安全渠道段，例如 `dev` |
| `DEPLOY_ENVIRONMENT` | `dev/test/staging/prod` |
| `FLAVOR` | 可选安全段 |
| `BUILD_TARGET` | exact Unity `BuildTarget`；当前 Player service支持Android/iOS |
| `PLAYER_VERSION` | 安全版本段，不自动取Jenkins build number |
| `PLAYER_BUILD_NUMBER` | 正整数 |
| `PROFILE` | fixture catalog中的profile id |
| `RUN_PLAYER_BUILD` | false只验证context/report；true执行responder、resource与Player build |
| `PUBLISH_RESOURCES` | 需要`RUN_PLAYER_BUILD=true`；上传immutable objects与descriptor，不切pointer |
| `PROMOTE_RESOURCES` | 需要同build `PUBLISH_RESOURCES=true`；等待人工审批后切pointer |
| `MINIMUM_CLIENT_BUILD` | staging时必须为正整数 |
| `MAXIMUM_CLIENT_BUILD` | staging时必须大于等于minimum |

推荐逐级启用：

1. `RUN_PLAYER_BUILD=false`，确认quality、Unity validate、report validation与archive。
2. `RUN_PLAYER_BUILD=true`、publish/promote均false，确认resource与Player artifacts。
3. 测试bucket中打开`PUBLISH_RESOURCES=true`，确认`staged-release.json`，同时确认current `publish.json`未变化。
4. 测试渠道打开`PROMOTE_RESOURCES=true`，在Jenkins审批页核对channel/platform/version后批准。
5. 客户端用受信public key读取新pointer、校验manifest hash并加载资源。

主job固定归档：channel report、Unity log、原始Unity quality XML、转换后的JUnit XML、quality log、resource/Player files，以及存在时的staged/promotion result。report validation会拒绝未知schema、失败状态矛盾、artifact路径逃逸、hash或size不匹配。

## Promote 与 rollback

Promote 只接受本次主job刚staged的版本。审批通过后工具会：

1. 从COS读取目标`channel-release.json`并用HEAD metadata回验descriptor hash/size。
2. 严格校验descriptor identity、client range、artifact key前缀与唯一manifest。
3. HEAD回验每个artifact的SHA-256 metadata与size。
4. HEAD读取current pointer ETag，不存在时使用create条件。
5. 使用当前RSA key重签Runtime pointer，并以`If-Match`或`If-None-Match: *`写入。

Rollback job 输入`CHANNEL`、`PLATFORM`、`TARGET_VERSION`。它不复制或删除对象，而是对目标历史descriptor执行同样的完整验证和重签；审批页确认后才进入credential scope。成功证据是`Build/Channel/rollback-result.json`。

## 故障恢复

| 现象 | 处理 |
|---|---|
| Quality XML缺失或非零 | 查看`quality-editmode.log`；不要跳过quality stage |
| SBP `PathTooLongException` | 缩短`GDK_UNITY_FIXTURE_ROOT`，不要打开全局跳过quality或改写bundle名称 |
| Unity exit 2/3/4/5/6 | 分别检查输入、pipeline、resource、Player、report；以report和Unity log为准 |
| Staging hash/size conflict | 目标version key已被不同内容占用；修正version，不覆盖历史对象 |
| Descriptor/artifact 404 | staging未完成或对象被外部删除；停止promote并恢复对象，不绕过验证 |
| Pointer ETag/412 conflict | 另一操作先更新了current；重新检查当前版本后重新发起并审批，不自动重试 |
| 私钥/key id失败 | 检查Jenkins file credential与客户端受信public key配置，不把PEM打印到日志 |
| Promote后客户端失败 | 立即运行rollback job切回上一个已知良好version；不删除失败版本，保留证据排查 |

## 唯一发布路径与上线 Gate

旧 Editor Resource Publisher 已移除，不再提供本地凭据、上传、delete、pointer 或迁移期 fallback。资源远端发布只能使用 Jenkins 的 COS stage、promote 与 rollback pipeline；在以下项目全部通过前不得面向生产渠道上线：

- 主job真实validate与Player build均成功，report/artifact可从Jenkins归档恢复。
- 测试bucket完成stage，确认immutable objects、hash metadata与descriptor。
- 测试渠道完成审批promote，客户端验证签名pointer和资源加载。
- 独立rollback job成功回到上一个版本，且412竞争会安全失败。
- Jenkins日志和archive扫描确认没有SecretId、SecretKey或private PEM。
- 运维方确认credential rotation、agent权限、artifact retention与故障联系人。

## 已知限制与注意事项

- 当前首个storage adapter只支持COS；不管理bucket、CDN或云账号生命周期。
- TeamCity可复用Unity command/report/release协议，但当前不提供Kotlin DSL。
- COS SDK调用为同步调用，开始后不能由cancellation token中断。
- 本地fixture通过不等于真实Jenkins/COS验收通过。

## 相关文档

- [Framework quickstart](framework-quickstart.md)
- [`Jenkinsfile`](../Jenkinsfile)
- [`Jenkinsfile.rollback`](../Jenkinsfile.rollback)
