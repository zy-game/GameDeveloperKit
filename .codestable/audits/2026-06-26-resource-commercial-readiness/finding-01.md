---
doc_type: audit-finding
audit: 2026-06-26-resource-commercial-readiness
finding_id: "security-01"
nature: security
severity: P1
confidence: high
suggested_action: cs-issue
status: ignored
---

# Finding 01：`ProjectSettings` 里明文持久化云发布密钥

> 用户已确认该项本轮略过：这些 `SecretId` / `SecretKey` 只用于本地便利，后续不会提交。

## 速答

`ResourcePublisherSettings` 把 `SecretId` / `SecretKey` 作为序列化字段写进 `ProjectSettings/GameDeveloperKitResourcePublisherSettings.asset`。如果该文件进入 VCS 或交付包会形成凭证泄漏风险；但本轮用户确认这些字段只作本地便利且不会提交，因此不作为当前可行动问题处理。

## 关键证据

- `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourcePublisherSettings.cs:17,75-79` — 设置文件固定写入 `ProjectSettings/GameDeveloperKitResourcePublisherSettings.asset`，`SaveSettings()` 直接序列化整个 `ScriptableObject` 到该路径。
- `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourcePublisherSettings.cs:130-173` — `PublisherChannel` 序列化 `m_SecretId` / `m_SecretKey`，并通过 `ToCredential()` 原样转成 `StorageCredential`。
- `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourcePublisherSettings.cs:50-66` — `LoadOrCreate()` 会自动读取并在不存在时创建该文件，意味着密钥会被默认落盘。

## 影响

只有在该设置资产被提交、复制给外部或进入构建机共享环境时才构成实际风险。当前本地使用场景按用户确认略过。

## 修复方向

暂不处理。若后续需要把发布工具交给团队或 CI 使用，再把密钥移出 Unity 序列化项目资产，改成 OS keychain、环境变量、外部未纳管配置，或至少做独立加密存储。

## 建议动作

本轮不进入 `cs-issue`。
