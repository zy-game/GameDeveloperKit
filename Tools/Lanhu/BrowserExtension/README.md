# GameDeveloperKit Lanhu Sync

首次使用时，在 Chrome 或 Edge 的扩展管理页开启开发者模式，并“加载已解压的扩展程序”，选择本目录。

安装后不需要复制脚本或打开开发者工具。Unity 的 UI Prefab Studio 会在本机 `127.0.0.1:18766` 建立一次同步任务，扩展只在已登录的蓝湖页面中读取设计数据并回传结果。Cookie、Token 和 Authorization 不会写入项目或本地设计缓存。
