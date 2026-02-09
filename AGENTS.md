<coding_guidelines>
<!-- OPENSPEC:START -->
# OpenSpec Instructions

These instructions are for AI assistants working in this project.

Always open `@/openspec/AGENTS.md` when the request:
- Mentions planning or proposals (words like proposal, spec, change, plan)
- Introduces new capabilities, breaking changes, architecture shifts, or big performance/security work
- Sounds ambiguous and you need the authoritative spec before coding

Use `@/openspec/AGENTS.md` to learn:
- How to create and apply change proposals
- Spec format and conventions
- Project structure and guidelines

Keep this managed block so 'openspec update' can refresh the instructions.

<!-- OPENSPEC:END -->

</coding_guidelines>

IMPORTANT:
- Double check the tools installed in the environment before using them.
- Never call a file editing tool for the same file in parallel.
- Always prefer the Grep, Glob and LS tools over shell commands like find, grep, or ls for codebase exploration.
- Always prefer using the absolute paths when using tools, to avoid any ambiguity.
- **DO NOT use the AskUser tool.** Instead, make reasonable assumptions based on context, implement solutions, and let the user provide feedback through natural conversation.