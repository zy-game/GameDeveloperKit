---
name: unity-editor
description: 与 Unity Editor 交互，操作场景、预制体、动画、材质、音频等资源。通过文件系统命令队列与 Unity 通信。
---

# Unity Editor Skill

通过 PowerShell 脚本与 Unity Editor 交互，执行各种编辑器操作。

## 使用方式

通过 Execute tool 调用 PowerShell 脚本（推荐方式）:

```powershell
Set-Location "<project_root>"; powershell -ExecutionPolicy Bypass -File "Library\CLI\unity-command.ps1" -Command "<command>" -Arguments '{\"key\":\"value\"}'
```

**重要**: 
- 使用 `Set-Location` 切换到项目根目录，用分号 `;` 连接命令
- `-Arguments` 中的 JSON 双引号必须转义为 `\"`
- 首次使用前需要在 Unity 中打开 GameDeveloperKit > CLI Service 窗口点击 Install 按钮
- Unity Editor 必须处于运行状态

## JSON 参数转义规则

PowerShell 中 JSON 参数必须转义双引号：
- 正确: `'{\"path\":\"Assets/Data/test.asset\"}'`
- 错误: `'{"path":"Assets/Data/test.asset"}'`

## 可用命令

### 场景操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_list_scenes` | 列出项目中的场景 | 无 | |
| `unity_get_scene_info` | 获取场景详情 | `path` | |
| `unity_open_scene` | 打开场景 | `path` | |
| `unity_save_scene` | 保存当前场景 | 无 | |
| `unity_create_scene` | 创建场景 | `name`, `path` | |
| `unity_delete_scene` | 删除场景 | `path` | |

### 预制体操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_list_prefabs` | 列出预制体 | 无 | `path` |
| `unity_get_prefab_info` | 获取预制体详情 | `path` | |
| `unity_create_prefab` | 创建预制体 | `name`, `path` | |
| `unity_update_prefab` | 更新预制体属性 | `path` | `properties` |
| `unity_delete_prefab` | 删除预制体 | `path` | |
| `unity_instantiate_prefab` | 实例化预制体到场景 | `path` | `parent`, `position` |
| `unity_apply_prefab_overrides` | 应用预制体覆盖 | `path` | |
| `unity_revert_prefab_overrides` | 还原预制体覆盖 | `path` | |

### 动画操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_list_animations` | 列出动画剪辑 | 无 | `path` |
| `unity_get_animation` | 获取动画详情 | `path` | |

### 材质操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_list_materials` | 列出材质 | 无 | `path` |
| `unity_get_material` | 获取材质详情 | `path` | |
| `unity_create_material` | 创建材质 | `name`, `path` | `shader` |
| `unity_update_material` | 更新材质属性 | `path`, `properties` | |
| `unity_delete_material` | 删除材质 | `path` | |

### 纹理操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_list_textures` | 列出纹理 | 无 | `path`, `type` |
| `unity_get_texture` | 获取纹理详情 | `path` | |
| `unity_update_texture` | 更新纹理导入设置 | `path` | `textureType`, `maxSize` |

### 音频操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_list_audio` | 列出音频剪辑 | 无 | `path` |
| `unity_get_audio` | 获取音频详情 | `path` | |
| `unity_update_audio` | 更新音频导入设置 | `path` | |

### 资源操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_search_assets` | 搜索资源 | `filter` | `path` |
| `unity_get_asset` | 获取资源详情 | `path` | |
| `unity_delete_asset` | 删除资源 | `path` | |
| `unity_move_asset` | 移动资源 | `source`, `destination` | |
| `unity_copy_asset` | 复制资源 | `source`, `destination` | |
| `unity_rename_asset` | 重命名资源 | `path`, `newName` | |
| `unity_find_references` | 查找资源引用 | `path` | |

### GameObject 操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_list_gameobjects` | 列出场景中的 GameObject | 无 | `path` |
| `unity_get_gameobject` | 获取 GameObject 详情 | `path` | |
| `unity_create_gameobject` | 创建 GameObject | `name` | `primitiveType`, `parent` |
| `unity_update_gameobject` | 更新 GameObject | `path` | `newName`, `tag`, `layer`, `active` |
| `unity_delete_gameobject` | 删除 GameObject | `path` | |
| `unity_add_component` | 添加组件 | `path`, `componentType` | |
| `unity_set_transform` | 设置 Transform | `path` | `position`, `rotation`, `scale` |
| `unity_get_component` | 获取组件属性 | `path`, `componentType` | |
| `unity_set_component` | 设置组件属性 | `path`, `componentType`, `properties` | |

### ScriptableObject 操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_list_scriptable_objects` | 列出 ScriptableObject | 无 | `type`, `path` |
| `unity_get_scriptable_object` | 获取 ScriptableObject 详情 | `path` | |
| `unity_create_scriptable_object` | 创建 ScriptableObject | `type`, `name`, `path` | |
| `unity_update_scriptable_object` | 更新 ScriptableObject | `path`, `fields` | |
| `unity_delete_scriptable_object` | 删除 ScriptableObject | `path` | |

### 控制台操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_get_console_logs` | 获取控制台日志 | 无 | `type`, `count` |
| `unity_clear_console` | 清空控制台 | 无 | |
| `unity_log` | 写入日志 | `message` | `type` |

### 编辑器操作
| 命令 | 说明 | 必填参数 | 可选参数 |
|------|------|----------|----------|
| `unity_get_editor_state` | 获取编辑器状态 | 无 | |
| `unity_set_play_mode` | 设置播放模式 | `playing` | |
| `unity_set_pause` | 设置暂停 | `paused` | |
| `unity_step_frame` | 单步执行一帧 | 无 | |
| `unity_select_objects` | 选择对象 | `paths` | |
| `unity_focus_gameobject` | 聚焦 GameObject | `path` | |
| `unity_compile_scripts` | 编译脚本 | 无 | |
| `unity_refresh_assets_force` | 强制刷新资源 | 无 | |

## 示例

### 列出所有动画
```powershell
Set-Location "E:\demo_Neverending Nightmares"; powershell -ExecutionPolicy Bypass -File "Library\CLI\unity-command.ps1" -Command "unity_list_animations" -Arguments '{}'
```

### 获取 ScriptableObject 详情
```powershell
Set-Location "E:\demo_Neverending Nightmares"; powershell -ExecutionPolicy Bypass -File "Library\CLI\unity-command.ps1" -Command "unity_get_scriptable_object" -Arguments '{\"path\":\"Assets/Data/Abilities/Attack/Ability_Fireball.asset\"}'
```

### 搜索特定类型资源
```powershell
Set-Location "E:\demo_Neverending Nightmares"; powershell -ExecutionPolicy Bypass -File "Library\CLI\unity-command.ps1" -Command "unity_search_assets" -Arguments '{\"filter\":\"t:AbilityBase\",\"path\":\"Assets/Data/\"}'
```

### 创建 GameObject
```powershell
Set-Location "E:\demo_Neverending Nightmares"; powershell -ExecutionPolicy Bypass -File "Library\CLI\unity-command.ps1" -Command "unity_create_gameobject" -Arguments '{\"name\":\"MyCube\",\"primitiveType\":\"Cube\"}'
```

## 注意事项

1. Unity Editor 必须处于运行状态
2. 命令超时默认为 30 秒，可通过 `-Timeout` 参数调整
3. 返回结果为 JSON 格式
4. 如果命令失败，会返回 `{"success":false,"error":"..."}`
5. JSON 参数中的双引号必须转义为 `\"`
