# Architecture Design — Unity ScriptableObject Data & Balance Tool

Working name only — project is not yet named. Referred to below as "the tool."

## Problem

Solo and small-team Unity developers who use ScriptableObject-driven ("data as config, not hardcoded logic") game design accumulate large sets of structured content — items, loot tables, drop rates, monster stats, progression curves — that are tedious to browse, edit, and validate in Unity's default per-asset Inspector, and whose emergent balance (rarity feel, drop frequency, difficulty pacing) is invisible until manual playtesting.

Existing tools solve pieces of this but not the whole thing:
- **Odin Inspector** (paid) improves per-asset editing UX generically, but isn't simulation-aware and has no free full-featured equivalent.
- **Machinations.io** and **Puida's Loot Table Designer** (free/freemium) simulate balance, but operate on data re-entered by hand into a disconnected tool — the model and the real game data drift out of sync as the game is tuned.
- Existing Unity MCP servers (official Unity MCP, CoplayDev/unity-mcp, AnkleBreaker-Studio/unity-mcp-plugin, IvanMurzak/Unity-MCP, CoderGamester/mcp-unity) provide generic Editor automation and basic ScriptableObject CRUD, but none provide validated database-style editing or simulation-as-a-tool.

The tool closes this loop: one real data source, browsable/editable/validatable as a database, with simulation run directly against that same data — never a second copy anywhere.

Grounded in (dogfooding, not the spec source) *Adventure Dreams*, a solo Unity RPG already using this pattern: 6 rarity tiers, 45 creature-specific drop tables, drop rates scaled by floor depth/elite/boss multiplier.

## Design principles

1. **Generic by construction.** Nothing may be hardcoded to Adventure Dreams' specific vocabulary (rarity tiers, elite/boss multipliers, creature-type mappings). Every such concept must be expressible as configuration, so a different Unity dev's differently-shaped data model works too.
2. **One real data source.** Editing, validation, and simulation all read/write through the same pipeline against the same live project data — never a re-entered or cached copy that can drift.
3. **UI-agnostic core, compiler-enforced.** The data model, validation, and simulation logic have zero dependency on any Editor GUI code, enforced by Unity assembly definitions rather than convention — so a future MCP server is an additional thin client, not a rewrite.
4. **Headless-capable by design.** Every core operation (read, validate, simulate) must be callable without an interactive Editor window from v1 onward, even though v1 only ships the interactive UI.
5. **Don't reimplement what must stay true.** Where an outcome depends on game code rather than data (e.g. full combat resolution), the tool must not approximate it externally — it's out of scope until it can hook into the real logic.

## Roadmap (scope, decided prior to this document)

- **v1** — Pillar 1 (data editor: database/table view over ScriptableObject collections, search/filter, bulk-edit, structural validation) + Pillar 2 (loot/economy Monte Carlo simulation run against real data, visualized as rarity distribution / drop frequency).
- **v2** — Pillar 3 (power-curve comparison: player power vs. monster power across floor/difficulty progression), via a generic, configurable power-formula system — not hardcoded to any one game's formula.
- **v3** — MCP server interface exposing the same core library's read/validate/simulate operations to AI coding agents, with dry-run/preview support (AI write-access to game data is a bigger trust step than AI writing reviewed code).
- **Explicitly excluded, indefinitely:** full turn-by-turn combat simulation (RNG, cooldowns, AI behavior) — depends on game code, not just data; would need to hook into the real combat resolver rather than being reimplemented, if ever pursued.

## Repository & packaging

Standalone Unity Package in its own git repository from day one (not embedded in the Adventure Dreams repo). Adventure Dreams consumes it as a Package Manager dependency, the same way any other Unity dev would. This makes the genericity principle enforceable in practice, not just in intent, and matches how the Unity OSS ecosystem expects tools to be distributed.

Target environment: Unity 6 (6000.x), matching Adventure Dreams' own editor version, since that's the dogfooding target and Unity 6 is the current LTS-track line.

## Technology

C#/.NET throughout — core, Unity integration, and UI. This is treated as a constraint forced by the domain, not a preference: correctly reading/writing ScriptableObjects requires Unity's own serialization APIs (`AssetDatabase`, `SerializedObject`); reimplementing Unity's asset format in another language would reintroduce the same "drift from the real truth" risk the tool exists to avoid, and bridging to an out-of-process language for logic this simple (weighted sampling, curve math) would add IPC complexity with no payoff.

## Architecture

Four assembly-definition-separated modules; three built for v1/v2, the fourth (`Mcp`) added in v3 as a sibling to `UI`, not a rewrite of anything below it.

```
Core            (plain C#, zero UnityEngine/UnityEditor reference)
  ^
Core.Unity      (Editor-only; bridges Unity <-> Core)
  ^
UI              (UI Toolkit; consumes Core.Unity)      [v3: Mcp also consumes Core.Unity]
```

### `Core`

Generic data model and math, with no knowledge that Unity exists. Compiles and unit-tests as a plain .NET class library — runnable via `dotnet test`, no Unity install required for this layer.

- **`Schema` / `FieldDescriptor`** — generic description of a data type's fields (numeric, string, enum, reference, weighted-entry-list, ...).
- **`ValidationRule`** — small, composable rules (`SumEquals`, `NoDuplicate`, `RequiredAtLeastOnce`, ...). A project's actual rules (e.g. "rarity weights sum to 100") are *configured* from these primitives, never hardcoded as a named rule.
- **`WeightedTable`** — generalizes "entries with a weight, scaled by an arbitrary numeric axis" (floor depth, player level, dungeon depth, ...) — not fixed to "rarity scaled by floor."
- **`SimulationEngine`** — runs N trials against a `WeightedTable`, returns a `SimulationResult` (distribution counts, percentiles). Validates its own inputs (e.g. rejects an all-zero-weight table) rather than dividing by zero.

### `Core.Unity`

Editor-only assembly (`includePlatforms: ["Editor"]`). The only place that touches Unity's asset APIs.

- **`ScriptableObjectRepository`** — scans `AssetDatabase` for instances of a configured type, maps serialized fields into `Core`'s generic model via `SerializedObject`/reflection, writes edits back (`ApplyModifiedProperties` + `AssetDatabase.SaveAssets`).
- **`SchemaMapper`** — infers a `Schema` from a type's `[SerializeField]` members. Unmappable fields are marked "unsupported" rather than aborting the scan.
- **Batch-mode entry point** — a static method callable via `-executeMethod`, driving the same scan → map → validate/simulate pipeline with no UI, for CI and (later) the MCP server.

### `UI`

UI Toolkit, deliberately thin — binds `Core.Unity` outputs to visual elements, holds no logic of its own.

- **`DataGridWindow`** — sortable/filterable table view over a `DataCollection`, bulk-edit, inline validation errors.
- **`SimulationResultsWindow`** — charts for `SimulationResult` (rarity distribution histogram, drop-frequency-over-floor-depth).

## Data flow

**Browse/edit:** `DataGridWindow` opens → `ScriptableObjectRepository` scans `AssetDatabase` → `SchemaMapper` infers `Schema` → assets mapped into a `DataCollection` → rendered as a table. On edit: written back through the repository, then relevant `ValidationRule`s re-run and surfaced inline.

**Simulate:** user selects a `WeightedTable` in `SimulationResultsWindow` → the same repository/mapper path reads the real current data → builds `Core`'s `WeightedTable` → `SimulationEngine.Run(table, iterations)` (pure `Core`, fast, independently testable) → `SimulationResult` → rendered as a chart.

**Headless/CI:** the `Core.Unity` batch-mode entry point drives the identical scan → map → validate/simulate pipeline with no UI, writes results to a log, returns a non-zero exit code on validation failure. This is the same path the v3 MCP server will call.

There is exactly one path that reads real project data, shared by editing, simulation, and CI.

## Error handling

Three distinct categories, not conflated:

1. **Validation failures** (data violates a configured rule) — expected outcomes, represented as data (a list of issues) returned alongside the `DataCollection`, not thrown as exceptions. Consumed by the UI (inline display) and CI (reporting) alike.
2. **Mapping failures** (a field doesn't fit the model — unusual custom type, broken reference) — degrade gracefully. `SchemaMapper` marks the field "unsupported" and continues the scan rather than aborting; a tool that crashes on the first unusual ScriptableObject someone points it at loses trust immediately, and every other dev's data model will look different from Adventure Dreams'.
3. **Write-back conflicts** (asset changed externally — e.g. edited directly in the Inspector — while the grid had it open) — detected before applying and surfaced as a reload prompt, not silently overwritten.

Batch-mode/CI exit codes distinguish *validation failures* (data problem, should fail the build) from *tool errors* (unexpected bug) so a CI consumer can tell them apart.

## Testing strategy

- **`Core`** — plain .NET unit tests (`dotnet test`), no Unity required, fast enough to run continuously.
- **`Core.Unity`** — Unity Test Framework EditMode tests (package already available) against fixture ScriptableObject assets, runnable via the Editor Test Runner or batch mode (`-runTests`) in CI.
- **`UI`** — kept thin specifically so there's little to test; EditorWindow/UI Toolkit GUI automation is hard to do well. Manual smoke-testing here; automated investment concentrated in `Core` and `Core.Unity`.

## Open questions deferred to later phase-specific design passes

- v2: the concrete shape of the configurable power-formula system for Pillar 3.
- v3: MCP transport (in-process C# SDK vs. an Editor-embedded bridge process, matching how existing Unity MCP servers work) and the exact tool surface exposed.
- Project naming, UPM distribution mechanism (git URL vs. OpenUPM registry), license choice.
