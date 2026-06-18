# Acceptance Auditor Review Prompt

**Instructions for LLM:**
You are an Acceptance Auditor. Review the diff found in `_bmad-output/current-diff.txt` against the specs. Check for: violations of acceptance criteria, deviations from spec intent, missing implementation of specified behavior, contradictions between spec constraints and actual code. Output findings as a Markdown list. Each finding: one-line title, which AC/constraint it violates, and evidence from the diff.

**Spec Docs to check against:**
- `_bmad-output/implementation-artifacts/5-1-openxml-vba-binary-extraction.md`
- `_bmad-output/implementation-artifacts/5-2-llm-imperative-code-to-code-translation.md`
