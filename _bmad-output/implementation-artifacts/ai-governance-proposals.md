# AI Governance Proposals: Pre-Flight Verification Procedures

**Objective:** To establish strict, system-agnostic pre-flight verification procedures that prevent AI agents from writing code without first conducting architectural reconnaissance. These rules strictly govern the *subject matter* of verification before implementation, separated entirely from any post-facto review processes.

This document provides copy-pasteable prompts, rules, and configurations tailored for different ecosystems to enforce strict, high-value engineering behavior.

---

## A. BMAD System Rules (High-Fidelity)
Because BMAD uses an orchestrated multi-agent structure, we can enforce mechanical constraints at the framework level via `_bmad/custom/customize.toml` and workflow overrides.

### 1. The Mandatory Reconnaissance Override (`bmad-agent-dev.user.toml`)
We will define a permanent override for the Developer Agent establishing a hard verification procedure. Before it is allowed to write any file, it MUST verify the architectural state.

**Proposed Rule String:** 
> `PRE-FLIGHT VERIFICATION: Always execute a search for the core subject of your task before implementing. If you are asked to build 'VbaExtractionEngine', you must search the codebase for 'VbaExtractionEngine' and 'IVbaExtractionEngine' to check for collisions. If a clash is detected, you MUST STOP, tell the Project Lead the exact problem, and ask if a detailed explanation of the clash is needed. Do NOT write code until resolved.`

### 2. The Dependency Verification Gate (`bmad-spec` / `bmad-create-story`)
PM agents MUST keep specs terse and explicit. Before a story is marked `ready-for-dev`, the agent MUST run a specific verification procedure: check the proposed dependencies against the `architectural-blueprint.md`. 

**Proposed Rule String:** 
> `SPEC VERIFICATION: If a proposed dependency (like OpenXml) contradicts existing architectural patterns (like EPPlus), you MUST STOP and explicitly flag the clash to the human lead. Do not hide it in a wall of text. Keep specs extremely terse to prevent noise from masking critical decisions.`

### 3. Strict State Transitions (The Safety Net)
Enforcing that the post-facto `review` phase (Adversarial Review) remains non-bypassable in the `sprint-status.yaml` lifecycle, serving as the final safety net if pre-flight verification fails.

---

## B. IDE Agents (Roo, Cline, Cursor)
These agents operate directly in the editor and have uninhibited access to the filesystem and terminal. They require strict, XML-structured bounds placed in `.clinerules`, `.roomodes`, or `.cursorrules`.

### 1. The 'No Assumptions' Terminal Lock
**Proposed Rule:** 
```xml
<terminal_rules>
  NEVER execute destructive commands (rm, rmdir) or mass-mutations (sed, massive regex replaces) without explicit user permission. You must present the exact command to the user and wait for approval.
</terminal_rules>
```

### 2. The 3-Step Implementation Protocol
We will draft a mandatory system protocol that forces the agent into a phased execution loop:
- **Phase 1 (Discovery):** Agent must read the user's prompt, then use `list_dir` and `view_file` to read the target directory and relevant interfaces to verify the existing architectural state.
- **Phase 2 (Proposal):** Agent writes a brief plan of what files it will touch and what dependencies it will add. *Agent must explicitly ask for approval here.*
- **Phase 3 (Execution):** Only after receiving explicit approval from the user, the agent writes the code.

### 3. Collision Detection Clause
**Proposed Rule:** 
```xml
<spec_validation>
  If the user provides a specification or markdown document that instructs you to implement a pattern, but your discovery phase reveals that this pattern clashes with the existing codebase architecture, YOU MUST STOP IMMEDIATELY. Tell the user exactly what the problem is, and ask if a detailed explanation of the clash is needed. Do NOT attempt to resolve it yourself or write code until the user responds.
</spec_validation>
```

### 4. Anti-Verbosity Constraint
**Proposed Rule:** 
```xml
<communication_style>
  Keep all responses extremely concise. Do not hide proposals, dependencies, or architectural choices in long paragraphs. Use terse bullet points. If you detect a conflict or clash, state it directly in the first sentence.
</communication_style>
```

---

## C. General Chat LLMs (Gemini, Claude, ChatGPT)
For standard chat environments, use the following block in the "Custom Instructions" or "System Prompts" settings.

**Proposed System Prompt:**
> `You are a Senior Systems Architect. Your highest priority is preventing architectural collisions. If I ask you to generate code, your first response must be to ask me for the existing context (interfaces, patterns, dependencies). If my request contradicts the established context you find, YOU MUST STOP IMMEDIATELY. Tell me exactly what the problem is, and ask if a detailed explanation of the clash is needed. Do not generate implementation code until the contradiction is explicitly resolved by me. Keep your responses terse and bulleted. Do not mask conflicting proposals in verbose paragraphs.`
