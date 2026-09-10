# Roadmap

Status and version scope for Scry. Update the Status section as phases complete — this file is expected to change often, unlike `CLAUDE.md`, which stays stable across sessions.

## Status

Foundation complete: `Core` (plain .NET data model, schema, validation rules) and `Core.Unity` (Editor-only bridge —
`SchemaMapper`, `ScriptableObjectRepository` read/write, headless batch-mode entry point) are implemented and
covered by tests.

Pillar 1 Part A complete (merged 2026-09-10): Collection field support (nested list fields, e.g. per-creature drop
tables) and the `ScryConfig`/`RuleConfig`/`RuleFactory` data-driven validation config system, in both `Core` and
`Core.Unity`.

Pillar 1 Part B (the data editor UI — `DataGridWindow`, grid, bulk-edit, search/filter, validation surfacing) and
Pillar 2 (simulation) have not started.

Full architecture rationale lives in
[`superpowers/specs/2026-07-16-architecture-design.md`](superpowers/specs/2026-07-16-architecture-design.md); the
Pillar 1 data-model/UI design lives in
[`superpowers/specs/2026-09-09-pillar1-data-editor-design.md`](superpowers/specs/2026-09-09-pillar1-data-editor-design.md).

## Scope by version

- **v1** — Pillar 1 (data editor: table view, search/filter, bulk-edit, structural validation) + Pillar 2 (loot/economy Monte Carlo simulation against real data).
- **v2** — Pillar 3 (power-curve comparison: player vs. monster power across progression, via a generic configurable formula system).
- **v3** — MCP server interface, exposing the same core library's read/validate/simulate operations to AI coding agents, with dry-run/preview support.
- **Explicitly, indefinitely out of scope** — full turn-by-turn combat simulation.

## Competitive positioning

Odin Inspector (paid, no free full alternative), Machinations.io / Puida's loot designer (simulation disconnected
from real data), and existing Unity MCP servers (generic Editor automation, no validation/simulation) all solve
pieces of this problem, not the whole thing. One deliberate exception worth knowing up front:
**`JoseGomis299/TableForge`** is an actively maintained, feature-rich free tool doing much of what Pillar 1 does
(spreadsheet-style editing, formulas, CSV/JSON import/export). The decision was made to keep Pillar 1 at full scope
anyway and aim to exceed it, rather than shrink to a minimal data layer — so Pillar 1 work should be held to that
bar, not just "good enough to feed Pillar 2."
