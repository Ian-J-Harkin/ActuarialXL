# 03 Compression and Chunking

The Vector Compression Engine is the mathematical gateway of the platform. It translates millions of unorganized Excel cells into a dense, logical, partitioned schema suitable for an LLM context window.

## 1. The Continuous Change-Point Algorithm
To collapse thousands of repeated rows (e.g., an actuarial table rolling forward 30 years), the system employs a deterministic change-point algorithm:
- Every row is parsed and its cell formulas extracted.
- An **Explicit Key-Sorted Signature** is generated: `string.Join("|", row.CellFormulas.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value))`.
- If Row 10's signature precisely matches Row 11's, the system groups them. When Row 12 presents a new rule (a change-point), the active partition is terminated and a new `VectorRangePartition` is spawned.

## 2. Dynamic Array Merging
Actuarial sheets often feature dynamic offsets. A formula in `N6` might reference `Col[+20]` or `Col[-1]`. The `VectorCompressionEngine` automatically traces the string boundaries. 
If an archetype uses varying chronological lookbacks across a single column (e.g. `1`, `-1`, `-2`), the Engine computes a merged `HashSet<int>` encompassing all discovered topological depths and assigns them to the global `ColumnDefinition`.

## 3. Boundary Safety & The Payload Envelope
Instead of feeding raw Excel syntax directly into an LLM (which incurs massive token costs and introduces unstructured hallucination risks), the `CompressedVectorBlock` schema creates a purely logical matrix. The final JSON passed to the LLM details only the unique mathematical relationships without overlapping structural noise.
