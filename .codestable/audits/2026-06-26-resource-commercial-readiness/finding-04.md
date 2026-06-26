---
doc_type: audit-finding
audit: 2026-06-26-resource-commercial-readiness
finding_id: "bug-04"
nature: bug
severity: P1
confidence: high
suggested_action: cs-issue
status: open
---

# Finding 04：删除远端版本后没有同步清理 `publish.json` 当前指针

## 速答

远端版本删除只删 bundle / manifest 文件，没有处理 `publish.json` 这类 current 指针。`SetCurrentVersion()` 会把当前版本写进 `publish.json`，而 runtime 启动时又先读这个指针再找 manifest。删掉当前版本后，指针就会变成悬空引用。

## 关键证据

- `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourceUploadPlanBuilder.cs:82-89` — `IndexKey(channel)` 固定生成 `publish.json` 键。
- `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourcePublisherWindow.cs:975-990` — `DeleteRemoteVersion()` 只删除 `item.UploadItems.Select(x => x.RemoteKey)`，没有删除或重写 `IndexKey(channel)`。
- `Assets/GameDeveloperKit/Editor/ResourcePublisher/ResourcePublisherWindow.cs:997-1010` — `SetCurrentVersion()` 会把 `ResourcePublishPointer` 序列化后写到 `IndexKey(channel)`。
- `Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.InitializeOperationHandle.cs:57-65` — runtime 先拿 `publishLocation` 读版本，再根据版本值解析 manifest 地址。

## 影响

如果删除的是当前正在对外提供的版本，线上客户端仍会沿着旧的 `publish.json` 找到被删掉的版本号，然后继续去拉不存在的 manifest / bundle，导致新启动或热更流程失败。

## 修复方向

删除版本时要么先把 current 指针迁到别的版本，要么原子性地清掉 `publish.json` 并阻止 runtime 继续读取悬空版本；至少不能只删包不改指针。

## 建议动作

`cs-issue`，因为这是远端发布链路的确定性一致性错误。
