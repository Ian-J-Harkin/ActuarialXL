# BMAD for Antigravity

This repository contains the official integration files for using the **BMAD Method (v6.0.4+)** with **Google Antigravity IDE**.

## Overview

[BMAD Method](https://bmad-method.org/) is an AI-driven agile development framework. While it natively supports IDEs like Claude Code and Cursor, this setup provides optimal compatibility with Google Antigravity, mapping all core BMAD tools and AI workflows into Antigravity slash commands.

## Included Features

* **`GEMINI.md`**: The system instruction configuration that tells Antigravity how to understand the BMAD folder structure, respect its standard configurations (`_bmad/core/config.yaml`), and interact with BMAD agents.
* **`.agent/workflows/`**: 10 comprehensive slash command workflows perfectly integrated into Antigravity UI:
  * `/bmad` - Activate the BMad Master agent
  * `/bmad-help` - Get AI advice on your next agile step
  * `/bmad-brainstorming` - Interactive AI brainstorming sessions
  * `/bmad-party-mode` - Multi-agent collaborative discussions
  * `/bmad-editorial-review-prose` - Review documents for clarity & tone
  * `/bmad-editorial-review-structure` - Propose structural doc improvements
  * `/bmad-review-adversarial-general` - Critical adversarial review
  * `/bmad-review-edge-case-hunter` - Find unhandled code edge cases
  * `/bmad-index-docs` - Generate/update folder indexes
  * `/bmad-shard-doc` - Split large documents into smaller files

## Installation

1. First, install the BMAD Master Core in your project directory:
   ```bash
   npx bmad-method install
   ```
   *(During IDE integration prompts, you can simply press enter to skip - this repo provides the Antigravity integration).*

2. Copy the contents of this repository into your project root:
   * Drop `GEMINI.md` into your project root.
   * Drop the `.agent` folder into your project root.

3. Restart Antigravity or refresh the window.

4. Start building! Type `/bmad-help` or `/bmad` to get your bearings.
