# editor-mcp-service Specification

## Purpose
TBD - created by archiving change add-unity-mcp-service. Update Purpose after archive.
## Requirements
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

### Requirement: Scene Resource Operations
The system SHALL support complete CRUD operations for Unity Scene assets through MCP tools.

#### Scenario: Create new scene
- **WHEN** AI assistant calls `unity_create_scene` tool with parameters `{name: "TestScene", path: "Assets/Scenes/"}`
- **THEN** a new scene file is created at the specified path
- **AND** the scene is registered in the AssetDatabase
- **AND** returns success with the scene GUID and path

#### Scenario: List all scenes in project
- **WHEN** AI assistant calls `unity_list_scenes` tool with optional filter `{path: "Assets/Scenes/"}`
- **THEN** returns an array of scene information including name, path, GUID, and size
- **AND** only includes scenes matching the filter if provided

#### Scenario: Open and read scene information
- **WHEN** AI assistant calls `unity_open_scene` tool with `{path: "Assets/Scenes/TestScene.unity"}`
- **THEN** the scene is loaded in the editor
- **AND** returns scene metadata including root GameObjects count and dependencies

#### Scenario: Delete scene asset
- **WHEN** AI assistant calls `unity_delete_scene` tool with `{path: "Assets/Scenes/TestScene.unity"}`
- **THEN** the scene file is moved to trash
- **AND** AssetDatabase is refreshed
- **AND** returns success confirmation

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

### Requirement: ScriptableObject Resource Operations
The system SHALL support complete CRUD operations for ScriptableObject assets through MCP tools.

#### Scenario: Create ScriptableObject instance
- **WHEN** AI assistant calls `unity_create_scriptable_object` tool with `{type: "GameDeveloperKit.Combat.AbilityBase", name: "FireballAbility", path: "Assets/Data/"}`
- **THEN** a new ScriptableObject instance of the specified type is created
- **AND** the asset is saved at the specified path
- **AND** returns the asset GUID, path, and type information

#### Scenario: Read ScriptableObject data
- **WHEN** AI assistant calls `unity_get_scriptable_object` tool with `{path: "Assets/Data/FireballAbility.asset"}`
- **THEN** returns the ScriptableObject's serialized fields as JSON
- **AND** includes type information and metadata

#### Scenario: Update ScriptableObject fields
- **WHEN** AI assistant calls `unity_update_scriptable_object` tool with `{path: "Assets/Data/FireballAbility.asset", fields: {AbilityName: "Fireball", Cooldown: 5.0}}`
- **THEN** the specified fields are updated using SerializedObject API
- **AND** changes are saved to the asset
- **AND** returns success with updated field values

#### Scenario: List ScriptableObjects by type
- **WHEN** AI assistant calls `unity_list_scriptable_objects` tool with `{type: "GameDeveloperKit.Combat.AbilityBase"}`
- **THEN** returns all ScriptableObject assets of the specified type
- **AND** includes name, path, GUID, and type for each asset

#### Scenario: Delete ScriptableObject asset
- **WHEN** AI assistant calls `unity_delete_scriptable_object` tool with `{path: "Assets/Data/FireballAbility.asset"}`
- **THEN** the asset is moved to trash
- **AND** returns success confirmation

### Requirement: MCP Protocol Compliance
The system SHALL implement the Model Context Protocol specification for tool discovery and execution.

#### Scenario: Initialize MCP connection
- **WHEN** MCP client sends `initialize` request with client capabilities
- **THEN** server responds with server capabilities including supported protocol version
- **AND** server capabilities include `tools` feature

#### Scenario: List available tools
- **WHEN** MCP client sends `tools/list` request
- **THEN** server responds with all available Unity tools
- **AND** each tool includes name, description, and JSON schema for parameters

#### Scenario: Call tool with valid parameters
- **WHEN** MCP client sends `tools/call` request with `{name: "unity_create_scene", arguments: {name: "Test", path: "Assets/"}}`
- **THEN** server validates parameters against the tool's schema
- **AND** executes the operation on Unity's main thread
- **AND** returns the result or error in MCP format

#### Scenario: Handle invalid tool call
- **WHEN** MCP client sends `tools/call` request with invalid parameters
- **THEN** server returns an error response with code and descriptive message
- **AND** does not execute the operation

### Requirement: Resource Discovery
The system SHALL provide MCP resources interface for browsing Unity assets.

#### Scenario: List available resources
- **WHEN** MCP client sends `resources/list` request
- **THEN** server returns a list of resource URIs for all supported asset types
- **AND** URIs follow the pattern `unity://scenes/*`, `unity://prefabs/*`, `unity://scriptableobjects/*`

#### Scenario: Read resource content
- **WHEN** MCP client sends `resources/read` request with `{uri: "unity://prefabs/Assets/Prefabs/Player.prefab"}`
- **THEN** server returns the resource content as JSON
- **AND** includes metadata such as type, dependencies, and properties

### Requirement: Thread Safety and Error Handling
The system SHALL ensure all Unity API calls execute on the main thread and handle errors gracefully.

#### Scenario: Execute operation on main thread
- **WHEN** MCP server receives a request on the stdio thread
- **THEN** the operation is queued for execution on Unity's main thread using EditorApplication.delayCall
- **AND** the response is sent back after execution completes

#### Scenario: Handle AssetDatabase errors
- **WHEN** an asset operation fails (e.g., file already exists, invalid path)
- **THEN** server catches the exception
- **AND** returns an MCP error response with appropriate error code and message
- **AND** logs the error to Unity console

#### Scenario: Handle timeout for long operations
- **WHEN** an operation takes longer than the configured timeout (default 30 seconds)
- **THEN** server cancels the operation if possible
- **AND** returns a timeout error to the client

### Requirement: Configuration and Monitoring
The system SHALL provide an editor window for configuring and monitoring the MCP service.

#### Scenario: Configure service settings
- **WHEN** user opens the MCP Service window
- **THEN** displays current service status (Running/Stopped)
- **AND** provides controls for auto-start on editor launch
- **AND** allows configuration of log level (Info/Debug/Error)

#### Scenario: View request logs
- **WHEN** MCP server processes requests
- **THEN** logs are displayed in the service window in real-time
- **AND** each log entry shows timestamp, tool name, and result status
- **AND** user can filter logs by level or search by keyword

#### Scenario: Export configuration for MCP clients
- **WHEN** user clicks "Export Config" button
- **THEN** generates a JSON configuration file for Claude Desktop or other MCP clients
- **AND** includes the command to launch Unity with MCP server enabled
- **AND** saves the file to a user-specified location

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

