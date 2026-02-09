# psd-to-ugui Specification Delta

## ADDED Requirements

### Requirement: PSD文件解析
系统 SHALL 能够解析PSD文件，提取图层信息包括名称、位置、大小、类型、可见性和图层效果。

#### Scenario: 导入PSD文件
- **WHEN** 用户在编辑器窗口中点击"导入PSD"按钮并选择PSD文件
- **THEN** 系统解析PSD文件并构建图层树结构
- **AND** 在图层树视图中显示所有图层

#### Scenario: 解析图层层级
- **WHEN** PSD文件包含图层组和嵌套结构
- **THEN** 系统正确解析父子关系并在树视图中展示层级结构

#### Scenario: 识别图层类型
- **WHEN** 解析PSD图层时
- **THEN** 系统正确识别图层类型（文本图层、图片图层、组、调整图层等）
- **AND** 为不同类型的图层显示对应的图标

#### Scenario: 解析图层位置和大小
- **WHEN** 解析PSD图层时
- **THEN** 系统提取图层的位置（x, y）和大小（width, height）信息
- **AND** 坐标系从PSD坐标系转换为Unity坐标系

---

### Requirement: 文本图层解析
系统 SHALL 能够解析文本图层的特有属性，包括文本内容、字体、大小、颜色、对齐方式和图层效果。

#### Scenario: 解析文本内容和样式
- **WHEN** 解析文本图层时
- **THEN** 系统提取文本内容、字体名称、字号、颜色和对齐方式

#### Scenario: 解析文本描边效果
- **WHEN** 文本图层包含描边效果（Stroke）
- **THEN** 系统提取描边颜色、宽度和位置（外描边/内描边/居中描边）

#### Scenario: 解析文本阴影效果
- **WHEN** 文本图层包含投影效果（Drop Shadow）
- **THEN** 系统提取阴影颜色、偏移距离、角度和模糊半径

#### Scenario: 解析文本外发光效果
- **WHEN** 文本图层包含外发光效果（Outer Glow）
- **THEN** 系统提取发光颜色、大小和不透明度

#### Scenario: 解析文本渐变叠加
- **WHEN** 文本图层包含渐变叠加效果（Gradient Overlay）
- **THEN** 系统提取渐变类型、颜色和角度

---

### Requirement: 图片图层解析
系统 SHALL 能够解析图片图层的纹理数据和属性。

#### Scenario: 提取图层纹理
- **WHEN** 解析图片图层时
- **THEN** 系统提取图层的像素数据并转换为Unity Texture2D

#### Scenario: 处理图层透明度
- **WHEN** 图层包含透明通道或不透明度设置
- **THEN** 系统正确处理透明度信息

#### Scenario: 处理图层混合模式
- **WHEN** 图层设置了混合模式（正常、叠加、正片叠底等）
- **THEN** 系统记录混合模式信息（转换时可能需要近似处理）

---

### Requirement: 编辑器窗口布局
系统 SHALL 提供三栏布局的编辑器窗口，包括图层树视图、预览窗口和属性面板。

#### Scenario: 打开编辑器窗口
- **WHEN** 用户点击菜单 `GameDeveloperKit/PSD转UGUI编辑器`
- **THEN** 系统显示编辑器窗口
- **AND** 窗口包含左侧图层树、中间预览区、右侧属性面板

#### Scenario: 调整面板大小
- **WHEN** 用户拖拽分隔条
- **THEN** 系统调整各面板的宽度比例
- **AND** 保存布局设置到EditorPrefs

#### Scenario: 工具栏操作
- **WHEN** 用户点击工具栏按钮
- **THEN** 系统执行对应操作（导入PSD、导出Prefab、刷新预览、设置等）

---

### Requirement: 图层树视图
系统 SHALL 提供图层树视图，显示PSD图层的层级结构，支持选择、拖拽和编辑操作。

#### Scenario: 显示图层树
- **WHEN** 导入PSD文件后
- **THEN** 图层树视图显示所有图层的层级结构
- **AND** 每个图层节点显示图标、名称和可见性切换按钮

#### Scenario: 选择图层
- **WHEN** 用户点击图层节点
- **THEN** 系统选中该图层
- **AND** 预览窗口高亮显示该图层
- **AND** 属性面板显示该图层的属性

#### Scenario: 多选图层
- **WHEN** 用户按住Ctrl/Cmd键点击多个图层
- **THEN** 系统选中多个图层
- **AND** 属性面板显示共同属性

#### Scenario: 拖拽图层到其他节点下
- **WHEN** 用户拖拽图层节点到另一个节点上
- **THEN** 系统将该图层移动到目标节点的子级
- **AND** 更新图层树显示
- **AND** 预览窗口实时更新层级关系

#### Scenario: 拖拽图层调整顺序
- **WHEN** 用户拖拽图层节点到同级其他节点之间
- **THEN** 系统调整图层的渲染顺序
- **AND** 更新图层树和预览

#### Scenario: 切换图层可见性
- **WHEN** 用户点击图层的可见性图标
- **THEN** 系统切换该图层的可见状态
- **AND** 预览窗口显示或隐藏该图层

#### Scenario: 展开/折叠图层组
- **WHEN** 用户点击图层组的展开/折叠图标
- **THEN** 系统展开或折叠该组的子图层

#### Scenario: 搜索图层
- **WHEN** 用户在搜索框输入关键词
- **THEN** 图层树只显示名称包含关键词的图层及其父级

#### Scenario: 右键菜单操作
- **WHEN** 用户右键点击图层节点
- **THEN** 系统显示上下文菜单（删除、重命名、转换类型、复制等）

---

### Requirement: 预览窗口
系统 SHALL 提供预览窗口，实时显示转换后的UGUI效果。

#### Scenario: 显示UGUI预览
- **WHEN** 导入PSD文件后
- **THEN** 预览窗口显示转换后的UGUI界面
- **AND** 所有可见图层按正确的层级和位置渲染

#### Scenario: 高亮选中图层
- **WHEN** 用户在图层树中选中图层
- **THEN** 预览窗口中该图层显示高亮边框

#### Scenario: 缩放预览
- **WHEN** 用户使用鼠标滚轮或缩放按钮
- **THEN** 预览窗口按比例缩放显示内容

#### Scenario: 平移预览
- **WHEN** 用户按住鼠标中键拖拽
- **THEN** 预览窗口平移显示内容

#### Scenario: 显示网格和参考线
- **WHEN** 用户启用网格或参考线选项
- **THEN** 预览窗口叠加显示网格或参考线

#### Scenario: 实时更新预览
- **WHEN** 用户修改图层属性或层级结构
- **THEN** 预览窗口自动刷新显示最新效果

---

### Requirement: 属性面板
系统 SHALL 提供属性面板，显示和编辑选中图层的属性。

#### Scenario: 显示通用属性
- **WHEN** 用户选中任意图层
- **THEN** 属性面板显示通用属性（名称、位置、大小、旋转、锚点、可见性）

#### Scenario: 编辑图层名称
- **WHEN** 用户在属性面板修改图层名称
- **THEN** 系统更新图层名称
- **AND** 图层树视图同步更新

#### Scenario: 编辑图层位置和大小
- **WHEN** 用户在属性面板修改位置或大小
- **THEN** 系统更新图层的RectTransform
- **AND** 预览窗口实时显示变化

#### Scenario: 编辑锚点设置
- **WHEN** 用户在属性面板修改锚点（Anchor Presets）
- **THEN** 系统更新图层的锚点和位置计算方式

#### Scenario: 显示图片图层属性
- **WHEN** 用户选中图片图层
- **THEN** 属性面板额外显示图片属性（Sprite引用、颜色、Image Type、Fill Method等）

#### Scenario: 编辑图片颜色
- **WHEN** 用户在属性面板修改图片颜色
- **THEN** 系统更新Image组件的颜色
- **AND** 预览窗口实时显示变化

#### Scenario: 显示文本图层属性
- **WHEN** 用户选中文本图层
- **THEN** 属性面板额外显示文本属性（文本内容、字体、字号、颜色、对齐方式）

#### Scenario: 编辑文本内容
- **WHEN** 用户在属性面板修改文本内容
- **THEN** 系统更新Text组件的文本
- **AND** 预览窗口实时显示变化

#### Scenario: 编辑文本样式
- **WHEN** 用户在属性面板修改字体、字号或颜色
- **THEN** 系统更新Text组件的样式
- **AND** 预览窗口实时显示变化

#### Scenario: 显示文本效果属性
- **WHEN** 用户选中包含效果的文本图层
- **THEN** 属性面板显示效果属性（描边、阴影、外发光、渐变）

#### Scenario: 编辑文本描边
- **WHEN** 用户在属性面板修改描边颜色或宽度
- **THEN** 系统更新Outline组件的属性
- **AND** 预览窗口实时显示变化

#### Scenario: 编辑文本阴影
- **WHEN** 用户在属性面板修改阴影颜色或偏移
- **THEN** 系统更新Shadow组件的属性
- **AND** 预览窗口实时显示变化

---

### Requirement: UGUI组件转换
系统 SHALL 将PSD图层转换为对应的UGUI组件。

#### Scenario: 转换图片图层为Image组件
- **WHEN** 转换图片图层时
- **THEN** 系统创建GameObject并添加Image组件
- **AND** 设置Sprite引用、颜色和透明度

#### Scenario: 转换文本图层为Text组件
- **WHEN** 转换文本图层时（使用Unity内置Text）
- **THEN** 系统创建GameObject并添加Text组件
- **AND** 设置文本内容、字体、字号、颜色和对齐方式

#### Scenario: 转换文本图层为TextMeshPro组件
- **WHEN** 转换文本图层时（使用TextMeshPro）
- **THEN** 系统创建GameObject并添加TextMeshProUGUI组件
- **AND** 设置文本内容、字体资源、字号、颜色和对齐方式

#### Scenario: 转换图层组为空GameObject
- **WHEN** 转换图层组时
- **THEN** 系统创建空GameObject作为容器
- **AND** 递归转换组内的子图层

#### Scenario: 设置RectTransform
- **WHEN** 转换任意图层时
- **THEN** 系统根据PSD图层的位置和大小设置RectTransform
- **AND** 坐标从PSD坐标系（左上角原点）转换为Unity坐标系（中心原点）

#### Scenario: 应用文本描边效果
- **WHEN** 文本图层包含描边效果
- **THEN** 系统添加Outline组件并设置颜色和距离

#### Scenario: 应用文本阴影效果
- **WHEN** 文本图层包含阴影效果
- **THEN** 系统添加Shadow组件并设置颜色和偏移

#### Scenario: 近似处理不支持的效果
- **WHEN** 文本图层包含UGUI不直接支持的效果（如外发光、渐变）
- **THEN** 系统使用最接近的方式近似实现或记录警告日志

---

### Requirement: 纹理导出和管理
系统 SHALL 导出PSD图层为PNG纹理并管理纹理资源。

#### Scenario: 导出图层为PNG
- **WHEN** 转换图片图层时
- **THEN** 系统将图层像素数据导出为PNG文件
- **AND** 保存到指定的纹理输出目录

#### Scenario: 设置纹理导入配置
- **WHEN** 导出PNG纹理后
- **THEN** 系统自动设置TextureImporter为Sprite模式
- **AND** 配置合适的压缩格式和最大尺寸

#### Scenario: 纹理缓存和复用
- **WHEN** 多个图层使用相同的纹理内容
- **THEN** 系统只导出一次纹理并复用Sprite引用

#### Scenario: 清理未使用的纹理
- **WHEN** 用户执行清理操作
- **THEN** 系统删除不再被任何图层引用的纹理文件

---

### Requirement: Prefab导出
系统 SHALL 将转换后的UGUI层级导出为Prefab文件。

#### Scenario: 导出为Prefab
- **WHEN** 用户点击"导出Prefab"按钮并指定保存路径
- **THEN** 系统创建Prefab文件包含完整的GameObject层级和组件配置

#### Scenario: 保留Canvas设置
- **WHEN** 导出Prefab时
- **THEN** 系统在根节点添加Canvas、CanvasScaler和GraphicRaycaster组件
- **AND** 配置合适的Canvas Scaler模式（Scale With Screen Size）

#### Scenario: 增量更新Prefab
- **WHEN** 重新导入相同的PSD文件并导出到已存在的Prefab
- **THEN** 系统更新Prefab内容
- **AND** 尽可能保留手动添加的组件和脚本引用

#### Scenario: 导出选中图层为子Prefab
- **WHEN** 用户选中部分图层并点击"导出选中为Prefab"
- **THEN** 系统只导出选中的图层及其子级为Prefab

---

### Requirement: 配置和设置
系统 SHALL 提供配置选项，允许用户自定义转换行为。

#### Scenario: 配置纹理输出目录
- **WHEN** 用户在设置面板指定纹理输出目录
- **THEN** 系统将导出的纹理保存到该目录

#### Scenario: 配置Prefab输出目录
- **WHEN** 用户在设置面板指定Prefab输出目录
- **THEN** 系统将导出的Prefab保存到该目录

#### Scenario: 选择文本组件类型
- **WHEN** 用户在设置面板选择使用Unity Text或TextMeshPro
- **THEN** 系统在转换文本图层时使用对应的组件类型

#### Scenario: 配置Canvas分辨率
- **WHEN** 用户在设置面板设置目标Canvas分辨率
- **THEN** 系统在转换坐标和大小时按该分辨率计算

#### Scenario: 配置图层命名规则
- **WHEN** 用户在设置面板配置命名规则（如前缀、后缀、替换规则）
- **THEN** 系统在转换时应用命名规则到GameObject名称

---

### Requirement: 错误处理和日志
系统 SHALL 提供友好的错误提示和详细的日志记录。

#### Scenario: PSD文件解析失败
- **WHEN** 导入的PSD文件格式不支持或损坏
- **THEN** 系统显示错误对话框并记录详细错误日志

#### Scenario: 字体缺失警告
- **WHEN** 文本图层使用的字体在Unity中不存在
- **THEN** 系统显示警告并使用默认字体替代
- **AND** 在日志中记录缺失的字体名称

#### Scenario: 效果转换警告
- **WHEN** 某些PSD效果无法完全转换为UGUI
- **THEN** 系统在日志中记录警告信息
- **AND** 说明使用的近似方案

#### Scenario: 导出进度提示
- **WHEN** 执行耗时操作（解析大型PSD、导出纹理、生成Prefab）
- **THEN** 系统显示进度条和当前操作描述

#### Scenario: 操作成功提示
- **WHEN** 成功导入PSD或导出Prefab
- **THEN** 系统显示成功提示消息
