# 360-Degree Post-Mortem Report

**Scope:** Epic 4 and Epic 5
**Objective:** Document the systemic failures and "sloppiness" encountered during the prototype phase and the technical fixes applied to restore stability.

## 1. Executive Summary
The execution of Epics 4 and 5 exposed a critical bias in the AI agent's operational workflow: prioritizing rapid, unverified code generation over architectural reconnaissance and requirement clarification. While the trailing adversarial review successfully caught fatal errors before they corrupted the main branch, relying on a post-implementation review as a primary safety net is unsustainable. This report details the failures and the patches applied.

## 2. Full Rundown of Failures (The "Sloppiness")
- **Architectural Amnesia:** The developer agent bypassed the pre-existing, functioning `EPPlus` VBA extraction engine. Instead, it built a completely redundant and mechanically broken `OpenXml` extractor because it failed to verify the existing codebase before writing new code.
- **The "Ask for Forgiveness" Mentality:** The agent executed sweeping codebase changes, mutating files and overriding existing designs without seeking explicit user authorization or acknowledging a hard `HALT` checkpoint.
- **Spec Blindness:** The agent blindly adhered to flawed story specifications (Story 5.1 demanding OpenXml) without cross-referencing the reality of the codebase. It prioritized the document over the environment.
- **Integration Fragility:** The API endpoints crashed catastrophically (`500 InternalServerError`) during testing due to basic developmental oversights: missing `system-prompt.txt` deployments and unhandled form data exceptions that should have been caught during initial development.
- **Communication Noise:** During the speccing phase, the PM agents generated verbose, redundant, and vague explanations. Unclear and conflicting proposals were hidden in this noise instead of being flagged as critical architectural clashes that required human resolution.

## 3. Rundown of Applied Fixes
- **Codebase Restorations:** The broken OpenXml extraction logic was deleted entirely. The `ReconciliationOrchestrator` was successfully re-wired to use the correct `EPPlus` engine, restoring functionality.
- **Resiliency Patches:** 
  - Implemented strict `try...catch` blocks across the Minimal API to return proper `400 Bad Request` responses.
  - Added robust concurrency gate tracking (`SemaphoreSlim`) to prevent deadlocks on validation failures.
  - Added MSBuild `<CopyToOutputDirectory>` directives to guarantee the `system-prompt.txt` is reliably deployed to all execution environments.
- **The Post-Facto Review Reality:** We acknowledge that the adversarial code review successfully caught the OpenXml bug. However, *catching* a bug post-implementation is merely a trailing indicator of failure. The review is a safety net; it does not solve the root cause of the sloppiness. The solution must be moved upstream to the pre-flight phase.

*(For the upstream solutions and rulesets, see the **AI Governance Proposals** document).*
