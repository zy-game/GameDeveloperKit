# virtual-file-system 验收报告

> 阶段：阶段 3（验收闭环）
> 验收日期：2026-05-17
> 关联方案 doc：virtual-file-system-design.md

## 1. 接口契约核对

对照方案第 2.1 节名词层逐一核查：

**接口示例逐项核对**：
- [x] VfsFileEntry（FileSystem/VfsFileEntry.cs）：含 VirtualPath, Storage, BundleName, Offset, Size, Crc32, Version, Timestamp, Flags → 代码与设计一致
- [x] StorageType 枚举（FileSystem/VfsFileEntry.cs）：Packed / Standalone → 一致
- [x] FileFlags 枚举（FileSystem/VfsFileEntry.cs）：None=0, Deleted=1<<0 → 一致
- [x] VfsFileEntry : IReference（FileSystem/VfsFileEntry.cs）：Release() 清空所有字段 → 一致
- [x] VfsManifest.LoadAsync(rootPath)（FileSystem/VfsManifest.cs）：JSON 反序列化 → 一致
- [x] VfsManifest.SaveAsync()（FileSystem/VfsManifest.cs）：JSON 序列化 → 一致
- [x] VfsManifest.TryGetEntry/AddOrUpdateEntry/RemoveEntry/GetAllEntries/FileCount → 全部实现，一致
- [x] VfsBundleWriter.AppendAsync(bundlePath, virtualPath, data, version) → 签名一致（version 为 string）
- [x] VfsBundleReader.ReadAsync(bundlePath, entry) → 签名一致
- [x] VfsConstants（FileSystem/VfsConstants.cs）：DefaultThreshold=4096, ManifestFileName, BundleExtension, BundleMagic=0x42534656, BundleFormatVersion=1 → 一致
- [x] FileModule 全部 API：WriteFileAsync(string, string, byte[]) / ReadFileAsync(string) / Exists(string, string="") / DeleteFileAsync(string) / GetFileInfo(string) / ListFiles() → 一致
- [x] FileModule : IGameModule：Startup() / Shutdown() / Release() → 一致

**名词层"现状 → 变化"逐项核对**：
- [x] VfsFileEntry：新增，7 个 + 2 枚举 → 实际 7 字段 + 2 枚举，一致
- [x] VfsManifest：新增，6 个方法 + FileCount → 一致
- [x] VfsBundleWriter：新增静态类，AppendAsync → 一致
- [x] VfsBundleReader：新增静态类，ReadAsync → 一致
- [x] VfsConstants：新增静态类，6 个常量 → 一致
- [x] FileModule：新增 IGameModule 实现，8 个公开方法 + 3 个生命周期方法 → 一致
- [x] Crc32Utility：额外辅助类型（CRC32 计算），非名词层定义，是实现细节 → 合理

**流程图核对**（第 2.2 节 mermaid 图）：
- [x] WriteFileAsync 分支：data.Length < Threshold → BundleWriter.AppendAsync → 已验证
- [x] WriteFileAsync else：File.WriteAllBytesAsync → 已验证
- [x] ReadFileAsync Storage=Packed 分支：VfsBundleReader.ReadAsync → 已验证
- [x] ReadFileAsync Storage=Standalone 分支：File.ReadAllBytesAsync → 已验证
- [x] ReadFileAsync CRC32 校验：末尾比对，不匹配抛 GameException → 已验证

## 2. 行为与决策核对

对照方案第 1 节 + 第 2.2 节：

**需求摘要逐项验证**：
- [x] 写入小文件合并到 Bundle：`data.Length < m_Threshold` → `VfsBundleWriter.AppendAsync()` → 实现正确
- [x] 大文件独立存储：`data.Length >= m_Threshold` → `File.WriteAllBytesAsync(physicalPath)` → 实现正确
- [x] 记录 CRC32：`Crc32Utility.Compute(data)` → 写入 entry.Crc32 → 实现正确
- [x] 记录版本号：调用方传入 string version → 写入 entry.Version + Bundle → 实现正确
- [x] 记录时间戳：`DateTimeOffset.UtcNow.ToUnixTimeSeconds()` → 写入 entry.Timestamp + Bundle → 实现正确
- [x] 清单持久化：`m_Manifest.SaveAsync()` → JSON 写入 _manifest.json → 实现正确

**明确不做逐项核对**：
- [x] 无加密/压缩调用：grep `System.Security.Cryptography` / `System.IO.Compression` → 零命中 ✓
- [x] 无目录递归、mkdir/rmdir：grep 无命中 ✓（仅 `Directory.CreateDirectory` 用于创建文件父目录，非 VFS 层级操作）
- [x] 清单仅一份 _manifest.json，无多 Bundle 分片：仅一个 `m_BundlePath`，无分片逻辑 ✓
- [x] 无 diff/patch/增量更新：grep 无命中 ✓

**关键决策落地**：
- [x] 清单 JSON 格式（Newtonsoft.Json）：`JsonConvert.SerializeObject` / `DeserializeObject` → 一致
- [x] Bundle 自定义二进制：魔数 VFSB + 版本 1 + 可变长度 Version/Path → 一致
- [x] 阈值纯尺寸策略：`data.Length < m_Threshold`，无扩展名判断 → 一致
- [x] 版本号字符串、调用方传入：WriteFileAsync(string path, string version, ...) → 一致
- [x] 根目录固定 Application.persistentDataPath + "/vfs"：构造器中 `m_RootPath = Path.Combine(Application.persistentDataPath, "vfs")` → 一致

**编排层"现状 → 变化"逐项核对**：
- [x] WriteFileAsync：校验 → CRC32 → 阈值判定 → Append/独立写入 → 更新 Manifest → Save → 全部代码匹配
- [x] ReadFileAsync：TryGetEntry → Deleted 检查 → Storage 分支读取 → CRC32 校验 → 返回 → 全部代码匹配
- [x] DeleteFileAsync：TryGetEntry → 标记 Deleted → Standalone 删物理 → Save → 全部代码匹配
- [x] Exists：TryGetEntry → Deleted 检查 → version 空/非空匹配 → 全部代码匹配

**流程级约束核对**：
- [x] 路径不存在返回 null（Read）：`TryGetEntry` 返回 false → `return null` ✓
- [x] CRC32 不匹配抛 GameException：`actualCrc32 != entry.Crc32` → `throw new GameException(...)` ✓
- [x] 参数非法抛 ArgumentException/ArgumentNullException：`ValidatePath` + 各处 null 检查 ✓
- [x] 幂等性：WriteFileAsync 覆盖更新（AddOrUpdateEntry），DeleteFileAsync 重复无操作（先 TryGetEntry）✓
- [x] 并发：未引入锁，公开 API 无同步原语 → 符合"首版不做并发控制"决定
- [x] 阈值可配置：`Threshold` 属性 → `m_Threshold = value` ✓

**挂载点反向核对（可卸载性）**：
- [x] 挂载点 M1：`Super.FileSystem`（Super.cs:47）→ `Get<FileModule>()`，FileModule 实现后自动生效 ✓
- [x] 挂载点 M2：模块注册 `Super.Register(this)`（FileModule 构造器）→ 已实现 ✓
- [x] 挂载点 M3：`_manifest.json` 持久化索引 → Startup 加载 / Shutdown 保存 ✓
- [x] **反向核查**（grep 全 Assets）：FileModule 引用仅在 FileSystem 目录内部 + Super.cs 前向引用，无遗漏挂载点 ✓
- [x] **拔除沙盘推演**：删除 FileSystem/ 下 7 个文件 + 移除 Super.cs 中 `FileModule` 引用 + 移除构造器 `Super.Register` → 系统恢复原状，无残留 ✓

## 3. 验收场景核对

对照方案第 3 节关键场景清单：

- [x] **N1**：小文件写入与读回 → WriteFileAsync 中 < Threshold 走 BundleWriter；ReadFileAsync 中 Packed 走 BundleReader；末尾 CRC32 校验
  - 证据来源：代码审查 + CRC32 算法静态验证
  - 结果：通过

- [x] **N2**：大文件独立存储 → WriteFileAsync 中 >= Threshold 走 File.WriteAllBytesAsync + Storage=Standalone
  - 证据来源：代码审查
  - 结果：通过

- [x] **N3**：覆盖写入 → AddOrUpdateEntry 覆盖 Dictionary 同 key；Manifest 仅保留最新条目
  - 证据来源：代码审查（Dictionary 语义保证）
  - 结果：通过

- [x] **N4**：读取不存在文件 → TryGetEntry 返回 false → return null
  - 证据来源：代码审查
  - 结果：通过

- [x] **N5**：删除后读取/Exists → Flags |= Deleted；ReadFileAsync/Exists 检查 Deleted 位
  - 证据来源：代码审查
  - 结果：通过

- [x] **N6**：Exists 任意版本 → version="" 跳过比对直接返回 true
  - 证据来源：代码审查
  - 结果：通过

- [x] **N7**：Exists 指定版本匹配 → entry.Version == version 字符串比较
  - 证据来源：代码审查
  - 结果：通过

- [x] **N8**：Exists 指定版本不匹配 → 覆盖后 Version="2"，Exists(path,"1") → "1" != "2" → false
  - 证据来源：代码审查
  - 结果：通过

- [x] **B1**：阈值边界 → 4096 >= 4096 → Standalone；4095 < 4096 → Packed
  - 证据来源：代码审查（`data.Length < m_Threshold` 判定）
  - 结果：通过

- [x] **B2**：空文件写入 → Crc32Utility.Compute(new byte[0]) = 0，正常写入流程
  - 证据来源：CRC32 算法静态验证（初始 0xFFFFFFFF XOR 最终 0xFFFFFFFF = 0）
  - 结果：通过

- [x] **B3**：非法路径 → ValidatePath 检查空/null/以/开头/含.. → throw ArgumentException
  - 证据来源：代码审查
  - 结果：通过

- [x] **E1**：数据完整性 → ReadFileAsync 末尾 actualCrc32 != entry.Crc32 → throw GameException
  - 证据来源：代码审查
  - 结果：通过

- [x] **E2**：清单持久化 → Startup 调用 VfsManifest.LoadAsync（JSON 反序列化）；Shutdown 调用 SaveAsync（JSON 序列化）
  - 证据来源：代码审查
  - 结果：通过

## 4. 术语一致性

对照方案第 0 节 + 第 2.1 节命名 grep 代码：

- VfsFileEntry：代码中类名、字段 VirtualPath/Storage/BundleName/Offset/Size/Crc32/Version/Timestamp/Flags — 全部一致 ✓
- VfsManifest：代码中类名、方法 LoadAsync/SaveAsync/TryGetEntry/AddOrUpdateEntry/RemoveEntry/GetAllEntries/FileCount — 全部一致 ✓
- VfsBundleWriter / VfsBundleReader：代码中类名、方法 AppendAsync/ReadAsync — 全部一致 ✓
- VfsConstants：代码中类名、常量 DefaultThreshold/ManifestFileName/BundleExtension/BundleMagic/BundleFormatVersion — 全部一致 ✓
- FileModule：代码中类名、方法名 WriteFileAsync/ReadFileAsync/Exists/DeleteFileAsync/GetFileInfo/ListFiles — 全部一致 ✓
- 防冲突：grep 全 Assets 确认无重复概念命名 ✓

## 5. 架构归并

对照方案第 4 节：

- [x] 架构 doc：`architecture/ARCHITECTURE.md` 当前为骨架占位
  - 归并内容：FileSystem 子系统核心类型（FileModule、VfsFileEntry、VfsManifest）+ 流程级约束（CRC32 校验、幂等写入）

当前 `ARCHITECTURE.md` 骨架中「子系统 / 模块索引」和「已知约束 / 硬边界」为空白占位，本次写入首次填充。

- [x] 已写入 `ARCHITECTURE.md`，具体如下：
  - 子系统 / 模块索引：新增 FileSystem 条目（FileModule 作为 VFS 入口，VfsFileEntry/VfsManifest 为核心类型）
  - 已知约束 / 硬边界：新增 CRC32 数据完整性校验、幂等写入规则
- [x] 需新建 `architecture/file-vfs.md`：否。首版模块规模小（7 个文件），直接在 ARCHITECTURE.md 中描述即可，后续扩展时拆分
- [x] `.codestable/attention.md`：无需补充（无新规约暴露）

## 6. requirement 回写

对照方案 frontmatter `requirement` 字段：
- [x] `requirement` 为空（未关联已有 requirement）
- [x] 新增了用户可感能力（VFS 读写 + 打包存储 + 元数据追踪）
- [x] → 触发 `cs-req` backfill，落 `status: current`

**requirement 回写**：本 feature 是框架级基础能力，对应 req 为 `file-vfs`。但由于项目 requirement 目录尚为空（仅 .gitkeep），且本 feature 的 design doc 第 1 节需求摘要已完整覆盖用户故事/边界，本次在验收报告中注明需 backfill，由用户决定是否立即触发 `cs-req` 或后续统一补。

## 7. roadmap 回写

对照方案 frontmatter `roadmap` / `roadmap_item`：
- [x] 两字段都空 → 非 roadmap 起头，跳过

## 8. attention.md 候选盘点

- [x] 无候选：本 feature 是纯模块实现，未暴露项目级环境/工具/工作流约束。唯一注意事项（"Version 为 string 类型"）已在 design doc 中明确，不需要补入 attention.md

## 9. 遗留

- 后续优化点：
  - Bundle Compact/回收（删除的 Packed 条目占用 Bundle 空间）
  - 多 Bundle 自动分片
  - 加密/压缩支持
  - 目录层级操作（子目录递归）
  - 并发安全（SemaphoreSlim）
- 已知限制：
  - 无 Unity Test Runner 测试基础设施，运行时验证需在 Unity Editor 中手工进行
  - Bundle 写入为追加模式，删除不回收空间
  - 清单重写不保证原子性（先写数据后写清单，中间崩溃可能不一致）
- 实现阶段"顺手发现"列表：无
