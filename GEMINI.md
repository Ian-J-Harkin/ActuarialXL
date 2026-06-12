# BMAD Method v6.0.4 - Antigravity Integration Instructions

> **System Instruction**: This project uses the BMAD Method framework. You must adhere to the following rules when interacting with this repository.

## Global Rules
1. **Always read `_bmad/core/config.yaml`** before executing any BMAD workflow or agent to load standard variables like `{user_name}` and `{output_folder}`.
2. Ensure you respond using the language specified in the config (`communication_language`).
3. Save generated artifacts to the configured `output_folder` (default: `_bmad-output/`).
4. Resolve the `{project-root}` placeholder in all `.agent/workflows` to the absolute path of this project directory.
5. Do not pre-load all agents/workflows. Only load `.agent/workflows/*.md` when invoked via a slash command by the user.

## Core Agents
- **🧙 BMad Master (`bmad-master.md`)**: The orchestration and initialization agent. Must execute exactly as specified in `_bmad/core/agents/bmad-master.md`.

## Slash Commands
When a user runs a slash command mapped in `.agent/workflows/` (e.g. `/bmad-agent-bmad-master` or `/bmad-help`), read the corresponding workflow file and follow its instructions exactly. Pay attention to `<agent-activation>` blocks.
