# Black Rain 架构总入口

> 状态：骨架（待填充）
> 创建日期：2026-05-17

## 1. 项目简介

Black Rain — Unity/C# GameDeveloperKit 框架项目

## 2. 核心概念 / 术语表

## 3. 子系统 / 模块索引

### FileSystem（VFS 虚拟文件系统）

入口：`FileModule`（`Assets/GameDeveloperKit/Runtime/FileSystem/FileModule.cs`），实现 `IGameModule`，通过 `Super.FileSystem` 访问。

核心类型：
- `VfsFileEntry` — 文件元数据（路径、CRC32、版本号、时间戳、存储方式）
- `VfsManifest` — JSON 清单索引，持久化到 `_manifest.json`

存储策略：
- 小文件（< 阈值 4096 字节）：合并写入单一 Bundle 文件（`.vfsb` 自定义二进制格式）
- 大文件（≥ 阈值）：独立文件存储在 VFS 根目录下

关键行为：写入自动 CRC32 校验、版本号调用方指定（string 类型，支持 `"1.0.1"` 语义版本）、软删除标记

## 4. 关键架构决定

## 5. 已知约束 / 硬边界

- **CRC32 数据完整性**：FileModule.ReadFileAsync 读取后强制 CRC32 校验，不匹配抛 `GameException`
- **幂等写入**：同路径多次 WriteFileAsync 为覆盖更新，Manifest 仅保留最新条目
- **根目录固定**：`Application.persistentDataPath + "/vfs"`，不可配置
- **首版不做并发控制**：所有公开 API 假定在主线程调用
