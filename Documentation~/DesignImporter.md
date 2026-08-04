# 设计稿转 Prefab

编辑器入口：`GameDeveloperKit / 设计稿转 Prefab`

## 支持的输入

- Figma：填写文件链接或 File Key，以及 Personal Access Token。工具调用 Figma `files` 和 `images` REST API，Frame/Component/Instance 生成页面，Text 生成 `TextMeshProUGUI`，矢量和位图节点下载为 Sprite。
- 蓝湖：在“配置表编辑器 > 全局设置 > UI Prefab Studio”中配置项目 URL。首次使用时也在该设置页安装浏览器同步桥，并在 Chrome 或 Edge 中加载随工具提供的扩展；之后打开工具直接点击“同步”即可。扩展复用已登录的蓝湖会话读取页面、图层和切图，Unity 自动建立本地缓存和版本差异；Cookie、Token 和 Authorization 不会写入项目或缓存。
- 清单：直接导入符合 `schemaVersion: 1.0` 的 JSON。清单可以由其他设计平台适配器生成。

## 生成规则

- 页面勾选决定批量生成范围；输出路径必须位于当前项目 `Assets` 下。
- 等比适配会居中留边，填充裁切会保持比例并裁切，拉伸会分别按宽高缩放。
- 资源以下载内容 SHA-256 去重。被多个页面引用或标记为“公共资源”的切图进入 `Common`，页面独占资源进入对应页面的 `Sprites`。
- 九宫格 Border 会写入 `TextureImporter.spriteBorder`，Border 不同的资源会生成不同变体文件名，避免导入设置互相覆盖。
- Prefab 默认包含 Screen Space Overlay Canvas、CanvasScaler 和 GraphicRaycaster；关闭“包含 Canvas”时生成可嵌入现有 Canvas 的 RectTransform 根节点。

## 蓝湖图层规则

- 蓝湖页面必须存在标注设计源。没有 `json_url` / `d2c_url` 的页面会被明确跳过，不再伪装成整页 Image。
- 当前分层导出器版本为 `3.0.0`。旧版只有单个整页 Image 的蓝湖清单会在导入时直接报错，避免再次生成单图 Prefab。
- 页面详情和图层源请求都有超时保护；单个异常页面会记录为跳过，不会卡住整个项目导出。
- 已标记为切图的图层会下载为独立 Sprite；文本生成 `TextMeshProUGUI`；分组保留为嵌套 `RectTransform`。
- Photoshop 图层坐标在设计源中是画布绝对坐标，导出器会转换成父级相对坐标，并反转 Photoshop 的前景优先顺序以匹配 Unity 的绘制顺序。
- 未标记切图的复杂位图、路径和图层效果无法由蓝湖直接导出为独立资源。需要在蓝湖中将它们标记为切图后重新执行脚本。

## 编辑与版本

- 工具启动时会自动读取当前蓝湖项目的最新本地缓存；“同步”会下载新版本并与上一个缓存版本比较，设计稿和图层右侧用 `NEW`、`UPDATE`、`DELETE` 标识差异。
- 设计稿列表与图层列表互斥。双击设计稿进入图层编辑，点击顶部“设计稿”面包屑返回列表；右侧 Inspector 始终编辑当前选中的图层。
- 拖动图层时，源图层显示“拖动中”，有效目标显示整行高亮和“作为子层级”或“插入到此行前”，指针附近同时显示源图层和当前落点；无效目标不会执行移动。
- 手工调整后的层级和位置保存在设计稿 mapping 中。后续同步会把 mapping 重新应用到同一稳定图层 ID；“恢复”可以删除当前设计稿的手工 mapping 并回到设计源层级。

## 已验证的测试项目

使用用户提供的“真人影视互动”项目验证：首次同步读取 69 个页面、2041 个节点和 542 个切图引用，其中“颜色”页面因蓝湖源没有可生成的图层被明确跳过。浏览器当前会话若提示重新登录，请重新登录后点击“同步蓝湖”。
