# 00 Executive Summary & System Context

## Overview
The **Actuarial Semantic Translation Engine** is a headless execution environment and interactive Web platform designed to bridge the gap between proprietary legacy Excel models (e.g., stochastic Monte Carlo, time-series, nested formulas) and modern, deterministic, penny-perfect C# subroutines.

By merging programmatic abstraction (Roslyn, EPPlus, ClosedXML) with Generative AI (LLMs), the system automates the structural translation of complex actuarial mathematical logic while enforcing rigorous verification limits (Variance $\le 0.00001$). 

## The "Triple Lock" of Mathematical Accuracy
Because LLMs are inherently non-deterministic, the architecture relies on a **Triple Lock** strategy to guarantee safety:
1. **Structural Anchoring:** The engine physically forces the LLM's spatial awareness. Instead of feeding the LLM an entire file, the system partitions datasets by exact structural change-points and passes isolated JSON boundary sets.
2. **Context Look-Aheads:** The LLM receives evaluated parameters corresponding not just to the active row, but explicitly populated `[-1]` (historical) and `[+1]` (future) boundary mappings to prevent logical breaks across recursive time-series.
3. **The Roslyn Iron Box:** The LLM's output is not blindly accepted. The text is parsed by `Microsoft.CodeAnalysis.CSharp`, compiled in a heavily sandboxed memory tier (`AssemblyLoadContext`), and executed mathematically against actual spreadsheet ground-truth arrays. If the compiled code varies from the Excel spreadsheet by more than 0.00001, the system rejects it and triggers an `ActuarialLogicLeakException`.

## Key Capabilities
- **Time-Series / Roll-Forward Translation:** Automatically determines recursion state and parses deep chronological lookbacks.
- **Stochastic Modelling / VBA Code-to-Code Translation:** Native ingestion of `.vbaProject.bin` arrays and flattening of procedural MS-OVBA macros into stateless implementations.
- **Mass Processing UI:** "Target Sheet" strategies protecting the network against multi-tenant execution.
- **Audit Governance:** An immutable SQLite ledger securing mathematical provenance, metadata IDs, and model parameters for external review.

The subsequent documents in this specification define the end-to-end reality of the production infrastructure.
