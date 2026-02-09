## ADDED Requirements

### Requirement: Material Resource Operations
The system SHALL support complete CRUD operations for Unity Material assets through MCP tools.

#### Scenario: List all materials
- **WHEN** AI assistant calls `unity_list_materials` tool with optional filter `{path: "Assets/Materials/", shader: "URP/Lit"}`
- **THEN** returns an array of material information including name, path, GUID, shader name, and render queue

#### Scenario: Create material with shader
- **WHEN** AI assistant calls `unity_create_material` tool with `{name: "NewMaterial", path: "Assets/Materials/", shader: "Universal Render Pipeline/Lit"}`
- **THEN** a new material asset is created with the specified shader
- **AND** returns the material GUID, path, and shader information

#### Scenario: Get material properties
- **WHEN** AI assistant calls `unity_get_material` tool with `{path: "Assets/Materials/Player.mat"}`
- **THEN** returns material metadata including shader name, all properties (colors, floats, textures), keywords, and render queue

#### Scenario: Update material properties
- **WHEN** AI assistant calls `unity_update_material` tool with `{path: "Assets/Materials/Player.mat", properties: {_BaseColor: {r:1,g:0,b:0,a:1}, _Smoothness: 0.5}}`
- **THEN** the specified shader properties are updated
- **AND** changes are saved to the asset

#### Scenario: Delete material asset
- **WHEN** AI assistant calls `unity_delete_material` tool with `{path: "Assets/Materials/Player.mat"}`
- **THEN** the material is moved to trash
- **AND** returns success confirmation

### Requirement: Texture Resource Operations
The system SHALL support read and import settings operations for Unity Texture assets through MCP tools.

#### Scenario: List all textures
- **WHEN** AI assistant calls `unity_list_textures` tool with optional filter `{path: "Assets/Textures/", type: "Sprite"}`
- **THEN** returns an array of texture information including name, path, GUID, dimensions, format, and texture type

#### Scenario: Get texture information
- **WHEN** AI assistant calls `unity_get_texture` tool with `{path: "Assets/Textures/Icon.png"}`
- **THEN** returns texture metadata including dimensions, format, import settings, and memory size

#### Scenario: Update texture import settings
- **WHEN** AI assistant calls `unity_update_texture` tool with `{path: "Assets/Textures/Icon.png", settings: {textureType: "Sprite", maxSize: 512, compression: "Normal"}}`
- **THEN** the texture import settings are updated
- **AND** the texture is reimported with new settings

### Requirement: Animation Resource Operations
The system SHALL support read operations for Unity Animation assets through MCP tools.

#### Scenario: List all animation clips
- **WHEN** AI assistant calls `unity_list_animations` tool with optional filter `{path: "Assets/Animations/"}`
- **THEN** returns an array of animation clip information including name, path, GUID, length, and frame rate

#### Scenario: Get animation clip information
- **WHEN** AI assistant calls `unity_get_animation` tool with `{path: "Assets/Animations/Walk.anim"}`
- **THEN** returns animation metadata including length, frame rate, loop settings, events, and animated properties

### Requirement: Audio Resource Operations
The system SHALL support read and import settings operations for Unity Audio assets through MCP tools.

#### Scenario: List all audio clips
- **WHEN** AI assistant calls `unity_list_audio` tool with optional filter `{path: "Assets/Audio/", loadType: "Streaming"}`
- **THEN** returns an array of audio clip information including name, path, GUID, length, channels, and sample rate

#### Scenario: Get audio clip information
- **WHEN** AI assistant calls `unity_get_audio` tool with `{path: "Assets/Audio/BGM.mp3"}`
- **THEN** returns audio metadata including length, channels, sample rate, import settings, and file size

#### Scenario: Update audio import settings
- **WHEN** AI assistant calls `unity_update_audio` tool with `{path: "Assets/Audio/BGM.mp3", settings: {loadType: "Streaming", compressionFormat: "Vorbis", quality: 0.7}}`
- **THEN** the audio import settings are updated
- **AND** the audio is reimported with new settings

### Requirement: Generic Asset Operations
The system SHALL support generic asset operations (copy, move, rename, find references) through MCP tools.

#### Scenario: Copy asset
- **WHEN** AI assistant calls `unity_copy_asset` tool with `{source: "Assets/Prefabs/Player.prefab", destination: "Assets/Prefabs/PlayerCopy.prefab"}`
- **THEN** the asset is copied to the destination path
- **AND** returns the new asset GUID and path

#### Scenario: Move asset
- **WHEN** AI assistant calls `unity_move_asset` tool with `{source: "Assets/Old/Item.prefab", destination: "Assets/New/Item.prefab"}`
- **THEN** the asset is moved to the destination path
- **AND** all references are updated automatically

#### Scenario: Rename asset
- **WHEN** AI assistant calls `unity_rename_asset` tool with `{path: "Assets/Scripts/OldName.cs", newName: "NewName"}`
- **THEN** the asset is renamed
- **AND** returns success with the new path

#### Scenario: Find asset references
- **WHEN** AI assistant calls `unity_find_references` tool with `{path: "Assets/Materials/Shared.mat"}`
- **THEN** returns a list of all assets that reference the specified asset
- **AND** includes the reference type and location

### Requirement: Console and Logging Operations
The system SHALL support console log operations through MCP tools.

#### Scenario: Get console logs
- **WHEN** AI assistant calls `unity_get_console_logs` tool with optional filter `{type: "Error", count: 50}`
- **THEN** returns recent console log entries matching the filter
- **AND** each entry includes timestamp, type (Log/Warning/Error), message, and stack trace

#### Scenario: Clear console
- **WHEN** AI assistant calls `unity_clear_console` tool
- **THEN** the Unity console is cleared
- **AND** returns success confirmation

#### Scenario: Write to console
- **WHEN** AI assistant calls `unity_log` tool with `{message: "Test message", type: "Warning"}`
- **THEN** the message is written to the Unity console with the specified log type

### Requirement: Editor State Operations
The system SHALL support editor state operations through MCP tools.

#### Scenario: Get editor state
- **WHEN** AI assistant calls `unity_get_editor_state` tool
- **THEN** returns current editor state including play mode, pause state, selected objects, and active scene

#### Scenario: Set play mode
- **WHEN** AI assistant calls `unity_set_play_mode` tool with `{playing: true}`
- **THEN** the editor enters or exits play mode as specified

#### Scenario: Set pause state
- **WHEN** AI assistant calls `unity_set_pause` tool with `{paused: true}`
- **THEN** the editor pauses or resumes play mode as specified

#### Scenario: Select objects
- **WHEN** AI assistant calls `unity_select_objects` tool with `{paths: ["Player", "Canvas/Button"]}`
- **THEN** the specified GameObjects are selected in the editor
- **AND** the selection is visible in the Hierarchy window

#### Scenario: Refresh AssetDatabase
- **WHEN** AI assistant calls `unity_refresh_assets` tool with optional `{importOptions: "ForceUpdate"}`
- **THEN** the AssetDatabase is refreshed with the specified options
- **AND** returns the number of assets processed

### Requirement: Code Execution (Sandboxed)
The system SHALL support limited C# code execution through MCP tools with safety restrictions.

#### Scenario: Execute safe code snippet
- **WHEN** AI assistant calls `unity_execute_code` tool with `{code: "return GameObject.FindObjectsOfType<Camera>().Length;"}`
- **THEN** the code is executed in a sandboxed environment
- **AND** returns the result as JSON

#### Scenario: Reject dangerous code
- **WHEN** AI assistant calls `unity_execute_code` tool with code containing file system operations, network calls, or process execution
- **THEN** the request is rejected with an error message explaining the restriction

### Requirement: GameObject Component Operations
The system SHALL support component property access for GameObjects in the current scene through MCP tools.

#### Scenario: Get component properties
- **WHEN** AI assistant calls `unity_get_component` tool with `{path: "Player", componentType: "Rigidbody"}`
- **THEN** returns all serialized properties of the component as JSON
- **AND** includes property names, types, and current values

#### Scenario: Set component properties
- **WHEN** AI assistant calls `unity_set_component` tool with `{path: "Player", componentType: "Rigidbody", properties: {mass: 2.0, drag: 0.5}}`
- **THEN** the specified component properties are updated
- **AND** changes are recorded in Undo system

## MODIFIED Requirements

### Requirement: MCP Server Lifecycle Management
Unity Editor SHALL provide an MCP (Model Context Protocol) server that can be started, stopped, and monitored through the editor interface. The architecture uses a two-layer design: an external MCP Proxy process (for stdio communication with MCP clients) and an internal HTTP server (for actual resource operations). **The server SHALL correctly locate its resources regardless of whether the package is installed in Assets or Packages directory.**

#### Scenario: Start MCP server from editor window
- **WHEN** user opens the MCP Service window and clicks "Start Server"
- **THEN** the HTTP server starts listening on the configured port (default 27182)
- **AND** the window displays "Running" status with port information

#### Scenario: Stop MCP server gracefully
- **WHEN** user clicks "Stop Server" while the server is running
- **THEN** the server completes pending requests
- **AND** closes all connections gracefully
- **AND** the window displays "Stopped" status

#### Scenario: Auto-start on editor launch
- **WHEN** Unity Editor starts and auto-start is enabled in settings
- **THEN** the HTTP server starts automatically in the background
- **AND** logs the startup status to the console

#### Scenario: Recover after Domain Reload
- **WHEN** Unity triggers a Domain Reload (e.g., after script compilation)
- **THEN** the HTTP server automatically restarts using InitializeOnLoad
- **AND** the task queue is loaded from persistent storage
- **AND** pending tasks continue execution
- **AND** clients can poll for task results as normal

#### Scenario: Handle Domain Reload during request processing
- **WHEN** a task is being processed and Domain Reload occurs
- **THEN** the task status remains "Running" in persistent storage
- **AND** after reload, the task is re-executed from the beginning
- **AND** the client receives the result when polling after reload completes

#### Scenario: Locate resources in package mode
- **WHEN** GameDeveloperKit is installed as a UPM package in another project
- **THEN** the server uses `PackageInfo.FindForAssembly()` to locate the package root
- **AND** correctly resolves paths to `unity_mcp_proxy.py` and other resources
- **AND** the InstallProxy function works correctly regardless of installation mode

#### Scenario: Locate resources in assets mode
- **WHEN** GameDeveloperKit is used directly in Assets folder (development mode)
- **THEN** the server falls back to `Application.dataPath` based path resolution
- **AND** all resource paths resolve correctly

### Requirement: Prefab Resource Operations
The system SHALL support complete CRUD operations for Unity Prefab assets through MCP tools, **including instantiation and override management**.

#### Scenario: Create prefab from GameObject
- **WHEN** AI assistant calls `unity_create_prefab` tool with `{name: "PlayerPrefab", path: "Assets/Prefabs/", template: "empty"}`
- **THEN** a new prefab asset is created with an empty GameObject
- **AND** returns the prefab GUID, path, and asset reference

#### Scenario: Get prefab information
- **WHEN** AI assistant calls `unity_get_prefab_info` tool with `{path: "Assets/Prefabs/PlayerPrefab.prefab"}`
- **THEN** returns prefab metadata including components, child count, and dependencies

#### Scenario: Update prefab properties
- **WHEN** AI assistant calls `unity_update_prefab` tool with `{path: "Assets/Prefabs/PlayerPrefab.prefab", properties: {name: "Player", tag: "Player"}}`
- **THEN** the prefab root GameObject properties are updated
- **AND** changes are saved to the asset
- **AND** returns success with updated information

#### Scenario: List all prefabs
- **WHEN** AI assistant calls `unity_list_prefabs` tool with optional filter `{type: "GameObject"}`
- **THEN** returns an array of all prefab assets with name, path, GUID, and type information

#### Scenario: Delete prefab asset
- **WHEN** AI assistant calls `unity_delete_prefab` tool with `{path: "Assets/Prefabs/PlayerPrefab.prefab"}`
- **THEN** the prefab is moved to trash
- **AND** returns success confirmation

#### Scenario: Instantiate prefab to scene
- **WHEN** AI assistant calls `unity_instantiate_prefab` tool with `{path: "Assets/Prefabs/Enemy.prefab", position: {x:0,y:0,z:0}, parent: "Enemies"}`
- **THEN** the prefab is instantiated in the current scene
- **AND** returns the instance path and ID

#### Scenario: Apply prefab overrides
- **WHEN** AI assistant calls `unity_apply_prefab_overrides` tool with `{instancePath: "Player", targetPrefab: "Assets/Prefabs/Player.prefab"}`
- **THEN** all overrides on the instance are applied to the prefab asset

#### Scenario: Revert prefab overrides
- **WHEN** AI assistant calls `unity_revert_prefab_overrides` tool with `{instancePath: "Player"}`
- **THEN** all overrides on the instance are reverted to match the prefab
