# AGENTS.md

## Project Overview

Black Rain is a Unity/C# project built around the `GameDeveloperKit` framework. The repository contains Unity assets under `Assets/`, package configuration under `Packages/`, project settings under `ProjectSettings/`, generated IDE project files (`*.csproj`, `Black Rain.sln`), and a Roslyn source generator in `GameDeveloperKit.Event.SourceGenerator/`.

## Repository Layout

- `Assets/GameDeveloperKit/Runtime/` - Runtime framework modules and APIs.
  - `Core/` - Common framework contracts, exceptions, reference pooling, and module interfaces.
- `Assets/GameDeveloperKit/Editor/` - Unity Editor-only tooling for GameDeveloperKit.
- `GameDeveloperKit.Event.SourceGenerator/` - Roslyn source generator targeting `netstandard2.0` with nullable enabled.
- `Packages/manifest.json` - Unity package dependencies including UniTask, Newtonsoft Json, URP, SBP, and Unity Test Framework.
- `ProjectSettings/` - Unity project settings; modify only when a task requires Unity configuration changes.
- `Library/`, `Temp/`, `obj/`, `bin/`, `.vs/`, `UserSettings/` - Generated/local folders; do not edit manually unless explicitly requested.

## Coding Conventions

- Use C# style already present in the project: 4-space indentation, braces on new lines, explicit access modifiers, and `var` where the surrounding code uses it.
- Runtime code uses namespace `GameDeveloperKit` from the asmdef root namespace.
- Private fields commonly use the `m_` prefix, for example `m_Handlers` and `m_Handles`.
- Prefer `UniTask`/`UniTaskVoid` over `Task` in Unity runtime async code, matching existing modules.
- Validate public inputs and throw `ArgumentNullException`, `ArgumentException`, or `GameFrameworkException` consistently with nearby code.
- Avoid adding new third-party dependencies unless requested and confirmed in `Packages/manifest.json` or a project file.
- Keep runtime code free of Editor-only APIs. Put Editor code under `Assets/GameDeveloperKit/Editor/` and rely on Editor-only asmdefs.
- Do not add broad comments or documentation unless requested; keep comments limited to non-obvious behavior.

## Unity and Asset Rules

- Do not manually create Unity `.meta` files; let Unity generate them automatically.
- Do not manually edit generated solution/project files unless the task specifically involves IDE/project generation or non-Unity projects such as the source generator.
- Do not modify `Library/`, `Temp/`, `Logs/`, or `UserSettings/Layouts/` as part of normal code changes.
- Be cautious with serialized assets, scenes, prefabs, and project settings; small text edits can affect Unity serialization.

## Build and Test Guidance

- Prefer Unity Test Framework for runtime/editor tests when validating Unity code.
- If Unity is available from the command line, use batch mode test runs appropriate to the target being changed, for example Editor or PlayMode tests.
- For source generator changes, validate with the .NET SDK against `GameDeveloperKit.Event.SourceGenerator/GameDeveloperKit.Event.SourceGenerator.csproj` when available.
- If command-line Unity or .NET tools are unavailable, report that verification could not be run and explain why.

## Agent Workflow

- Inspect the relevant files before editing and match local style and patterns.
- Keep changes focused on the user request; do not refactor unrelated modules.
- Before committing or pushing, inspect `git status`, staged diffs, and check for secrets or generated artifacts.
- Do not overwrite user changes. The current working tree may contain active modifications and untracked Unity assets.
