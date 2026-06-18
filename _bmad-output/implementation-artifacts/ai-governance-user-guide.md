# User Guide: Applying AI Governance Proposals

This guide explains exactly how to take the rules defined in the `ai-governance-proposals.md` document and actively apply them across your various AI agent ecosystems to enforce the pre-flight verification procedures.

---

## 1. Applying Rules to BMAD

BMAD's multi-agent system uses a hierarchical configuration structure. To enforce rules system-wide or per-agent, you must update the customization files located in the `_bmad/custom/` directory.

### A. Global Pre-Flight Verification
To ensure all BMAD agents follow the "Stop and Ask" rule and eliminate verbosity:
1. Navigate to `_bmad/custom/` in your project root.
2. If it does not exist, create a file named `customize.toml` (or `config.toml` depending on your BMAD version).
3. Add the rules from the Governance Proposals document under the appropriate agent instruction blocks or global system prompts. For example:
   ```toml
   [agent_instructions.global]
   pre_flight_rule = "PRE-FLIGHT VERIFICATION: Always execute a search for the core subject of your task before implementing. If a clash is detected, you MUST STOP, tell the Project Lead the exact problem, and ask if a detailed explanation of the clash is needed."
   ```

### B. Developer-Specific Overrides
To restrict the Developer Agent specifically:
1. In `_bmad/custom/`, create or open `bmad-agent-dev.user.toml`.
2. Add the mandatory reconnaissance override so the Developer Agent always searches before writing code.

---

## 2. Applying Rules to IDE Agents (Roo, Cline, Cursor)

IDE-integrated agents read specific markdown or rule files located at the root of your workspace to determine their system constraints. 

### A. For Roo and Cline
Roo and Cline look for specific instruction files to govern their behavior.
1. Create a file named `.clinerules` (for Cline) or configure your `.roomodes` (for Roo) at the root of your repository (`c:\Github\ActuarialXLpoc\`).
2. Open the `ai-governance-proposals.md` document.
3. Copy the XML blocks (`<terminal_rules>`, `<spec_validation>`, and `<communication_style>`) and paste them directly into your `.clinerules` file.
4. **Verification:** The next time you invoke Roo or Cline, they will automatically parse these XML blocks and enforce the 3-Step Implementation Protocol and the Collision Detection Clause.

### B. For Cursor
Cursor uses a global rules file to constrain its Composer and Chat features.
1. Create a file named `.cursorrules` at the root of your repository.
2. Paste the exact same XML blocks or plain-text rules from the Governance Proposals document into `.cursorrules`.
3. Cursor will now apply the Anti-Verbosity constraint and halt code generation if it detects an architectural clash during context discovery.

---

## 3. Applying Rules to General Chat LLMs (Gemini, Claude, ChatGPT)

When using standard web interfaces for LLMs, you must embed the governance rules into their persistent memory or system prompt settings so you don't have to repeat them every conversation.

### A. Gemini (Advanced/Studio)
1. If using Gemini AI Studio, place the **Proposed System Prompt** from the Governance document directly into the "System Instructions" box on the left-hand panel.
2. If using the standard Gemini web interface, you can paste the prompt into a "Saved Prompt" or include it as the first message in any new architectural thread.

### B. Claude (Anthropic)
1. Open Claude and navigate to "Projects".
2. Create a Project for "ActuarialXL".
3. In the Project's "Custom Instructions" section, paste the **Proposed System Prompt**.
4. Every chat within this Project will now strictly adhere to the "Stop and Ask" imperative.

### C. ChatGPT (OpenAI)
1. Click on your profile name and select **Customize ChatGPT**.
2. In the bottom box ("How would you like ChatGPT to respond?"), paste the **Proposed System Prompt**.
3. Ensure the toggle for "Enable for new chats" is turned on. ChatGPT will now act as the constrained Senior Systems Architect across all new conversations.
