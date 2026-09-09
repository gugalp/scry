# Pillar 1 — Data Editor UI Design

Extends the architecture in [`2026-07-16-architecture-design.md`](2026-07-16-architecture-design.md). That
document defines `Core`/`Core.Unity` foundations and sketches `UI` at a high level (`DataGridWindow`,
"deliberately thin, binds outputs to visual elements"). This document is the phase-specific design pass for
Pillar 1 referenced there, and revises three assumptions the architecture doc made before this data model existed:

1. The data model was flat (`FieldType`: `Numeric | String | Boolean | Enum | Reference | Unsupported`) with no
   list/nested-object support. Adventure Dreams' drop tables — the validation target — are lists of weighted
   entries per creature, so Pillar 1 cannot browse or edit the data that motivates the tool without extending
   this. This design adds a `Collection` field type to `Core` and the mapping/repository support for it in
   `Core.Unity`.
2. There was no config surface — `BatchEntryPoint` took a raw `-scryType` CLI argument, and nothing persisted
   "these are the types I track" or "these are my validation rules" as project data. This design adds a
   `ScryConfig` asset as that source of truth.
3. `UI` was assumed to have "little to test" because GUI automation is hard. That still holds for
   `EditorWindow`/tree-view wiring itself, but the *data-to-visual-element mapping* (which column shows which
   value, which cells get which validation icon) turns out to be non-trivial pure logic once Collection fields
   and validation surfacing are in scope, and is kept separately testable per Section 5 below.

## Why now, and why this scope

TableForge (see architecture doc's competitive section) already does mature spreadsheet-style editing over
ScriptableObjects. The deliberate bet, restated in CLAUDE.md, is that Pillar 1 matches or exceeds TableForge's
*editing* quality rather than shrinking to a minimal data layer — while formulas and CSV/JSON import/export
(TableForge features not attempted here) stay explicitly deferred, per Section 5. Pillar 2 (simulation) depends on
data actually being editable/validated first, which is why Pillar 1 goes first.

## 1. `Core` data model extensions

- **`FieldType`** gains `Collection`.
- **`FieldDescriptor`** gains an optional `ElementSchema` (a `Schema`), populated only when `Type == Collection`
  — the schema of one entry in the list.
- **`DataRecord`** values: `GetValue(fieldName)` on a `Collection` field returns `IReadOnlyList<DataRecord>`.
  Each entry is itself a `DataRecord` keyed by `ElementSchema`'s fields. This reuses the existing
  `DataRecord`/`Schema` shapes recursively rather than inventing a parallel "nested record" type.
- **Nested validation**: existing rule constructors (`SumEqualsRule`, `NoDuplicateRule`,
  `RequiredAtLeastOnceRule`) gain an optional `nestedField` parameter. When set, the rule evaluates against
  each parent record's `IReadOnlyList<DataRecord>` for that field instead of the top-level collection, and
  emits `ValidationIssue`s scoped to `parentRecordId` (and child index, for `NoDuplicateRule`-style per-entry
  issues). No new rule types are introduced — existing rules gain the ability to point one level down.

Only single-level nesting is modeled. `List<List<T>>` or deeper stays `Unsupported`, consistent with the
existing graceful-degradation principle (`SchemaMapper` already marks unrecognized fields "unsupported" and
continues rather than aborting the scan).

## 2. Config & rule authoring

- **`ScryConfig`** — a `ScriptableObject` asset (`Assets > Create > Scry > Config`), one per project by
  convention (multiple found = the window warns rather than silently picking one). Holds
  `List<TrackedCollection>`; each entry pairs a tracked type (stored as assembly-qualified name string,
  resolved at scan time via `Type.GetType`) with that collection's `List<RuleConfig>`.
- **`RuleConfig`** — an abstract `[Serializable]` base. Concrete subclasses (`SumEqualsRuleConfig`,
  `NoDuplicateRuleConfig`, `RequiredAtLeastOnceRuleConfig`) carry the typed parameters each rule needs
  (`Field`, `Target`, `Tolerance`, `GroupByField`, `NestedField`, ...). `List<RuleConfig>` uses
  `[SerializeReference]` so Unity's default Inspector renders each concrete subclass's own fields correctly
  without a custom `PropertyDrawer` — this is the mechanism that keeps rule authoring "default Inspector, no
  code" per the decision below, and is the riskiest new serialization behavior in this design (see Section 5
  testing).
- **`[ScryCollection]` attribute** — optional, placed on a `ScriptableObject` subclass. `ScryConfig`'s
  inspector gets a "Sync tracked types" button that scans via `TypeCache.GetTypesWithAttribute` and adds any
  missing attributed types as empty `TrackedCollection` entries. Discovery/convenience only — a type without
  the attribute can still be added manually, and the attribute never carries rules itself. This keeps "rules
  are data" intact: the attribute helps populate the list of *what* to track, never *how* to validate it.
- **`RuleFactory`** (`Core.Unity`) — static `ValidationRule Build(RuleConfig config)`, a switch over concrete
  `RuleConfig` types, each arm constructing the matching `Scry.Core.Rules.*` instance. Adding a new rule kind
  is three parts: one `Core.Rules` class, one matching `RuleConfig` subclass, one switch arm — same discipline
  the three existing rules already follow. (Rejected alternative: reflection-based auto-mapping akin to
  `SchemaMapper`'s field inference — more generic, but adds real complexity for only 3-5 rule kinds, and
  failures are harder to trace than an explicit switch.)

## 3. `Core.Unity` changes for Collection fields

- **`SchemaMapper.InferSchema`** — when a field's type is `List<T>` or `T[]` and `T` is a plain
  `[Serializable]` class (not a `UnityEngine.Object`), maps it as `FieldType.Collection` with
  `ElementSchema = InferSchema(typeof(T))`, recursing through the existing public + non-public-`[SerializeField]`
  walk on the element type. Anything else (`List<int>`, `Dictionary<,>`, deeper nesting) stays `Unsupported`.
- **`ScriptableObjectRepository.Scan`** — for a `Collection` field, walks the array `SerializedProperty`
  (`isArray`, `arraySize`, `GetArrayElementAtIndex`), building one nested `DataRecord` per element (id =
  `"{parentGuid}#{index}"`, since array elements have no independent GUID), reading each nested field via the
  existing `ReadValue` against the element's own `SerializedProperty` children.
- **`ScriptableObjectRepository.ApplyEdit`** — extended to accept a nested field path (parent field name +
  child index + child field name) to edit one cell inside an expanded sub-table row, resolving through
  `GetArrayElementAtIndex(index).FindPropertyRelative(childField)` before the existing `WriteValue`.
- **`AddCollectionEntry` / `RemoveCollectionEntry`** — new methods (not folded into `ApplyEdit`, since
  insert/remove don't fit its "write one field, return updated record" shape) using
  `InsertArrayElementAtIndex`/`DeleteArrayElementAtIndex`. Needed because drop tables need rows added, not just
  edited, for the sub-table UX to be useful.
- **Fingerprint/conflict detection is unchanged.** `AssetDatabase.GetAssetDependencyHash` already covers full
  asset content including array data, so nested edits are conflict-checked the same way top-level edits already
  are — no new mechanism needed here.

## 4. `UI` layer

- **`DataGridWindow`** — one `EditorWindow` (`Scry > Data Editor` menu item). Locates the project's
  `ScryConfig` via `AssetDatabase.FindAssets("t:ScryConfig")`. A tab strip lists `TrackedCollection` entries;
  selecting one triggers `ScriptableObjectRepository.Scan` and renders the result. One window, tabbed by
  collection — not one window per type — so there's a single place to look across a project's tracked types.
- **Grid widget: `MultiColumnTreeView`** (UI Toolkit), not `MultiColumnListView` — it's the one built-in widget
  supporting both virtualized/sortable columns and hierarchical expand/collapse rows, which Collection-field
  sub-tables need. One column per top-level `FieldDescriptor`, bound to the matching built-in control
  (`IntegerField`/`FloatField` for Numeric, `TextField` for String, `Toggle` for Boolean, `PopupField` for Enum,
  `ObjectField` for Reference). A `Collection` cell renders as a summary label (`"12 entries"`) with an expand
  arrow; expanding inserts child tree rows columned by `ElementSchema`.
- **Editing** — every write goes through `ScriptableObjectRepository.ApplyEdit` /
  `AddCollectionEntry`/`RemoveCollectionEntry`, wrapped in `Undo.RegisterCompleteObjectUndo(asset, ...)` before
  the write, so Ctrl+Z reverts it like any other Unity Editor edit.
- **Bulk-edit** — row checkboxes (shift-click range select) plus a toolbar: pick a field, enter a value,
  "Apply to N selected" writes through the same `ApplyEdit` path per row, with the whole batch wrapped in one
  `Undo.CollapseUndoOperations` group so it undoes as a single step.
- **Search/filter** — a toolbar search field filters visible rows by substring match across all top-level field
  values, client-side over the already-scanned `DataCollection` (no re-scan per keystroke). Each column header
  gets a type-appropriate filter affordance: text-contains (String), min/max (Numeric), checklist (Enum),
  tri-state (Boolean). Filtering only changes which rows render, never underlying data.
- **Validation surfacing** — after scan and after each edit, `RuleFactory`-built rules run against the
  `DataCollection` (top-level rules) and against each parent's nested list (nested-field rules). Matching cells
  get a colored icon overlay (red = Error, yellow = Warning) with the message as tooltip; a collapsible bottom
  panel lists all current issues, click-to-scroll/select (expanding the parent row first if the issue is on a
  nested field).
- **Write conflicts** — per the existing architecture doc: `ApplyEdit` throwing `WriteConflictException`
  surfaces as a reload-prompt dialog rather than silently overwriting; accepting re-scans just that record.

## 5. Testing strategy

- **`Core`** (`dotnet test`): `Collection` `FieldType`; `DataRecord` holding nested records; rule evaluation
  against a nested field (e.g. `SumEqualsRule` with `nestedField` set) — all plain-.NET testable.
- **`Core.Unity`** (Unity EditMode tests): `SchemaMapper` detecting `List<T>`/`T[]` of `[Serializable]` classes
  and recursing into `ElementSchema`; `ScriptableObjectRepository.Scan`/`ApplyEdit`/`AddCollectionEntry`/
  `RemoveCollectionEntry` against a fixture asset with a nested list field; `RuleFactory.Build` for each rule
  kind; `ScryConfig` round-tripping through Unity serialization with a mixed `List<RuleConfig>` — this last one
  specifically verifies `[SerializeReference]` polymorphism actually works, the riskiest new serialization
  mechanism in this design.
- **`UI`** — `EditorWindow`/tree-view wiring itself stays manual-smoke-tested (GUI automation remains out of
  scope, per the architecture doc). The data-to-visual-element mapping — which column shows which value from a
  `DataRecord`, which cells get which `ValidationIssue` icon — is kept as small, pure, testable
  functions/classes rather than inline in `DataGridWindow` callbacks, applying "thin UI, logic elsewhere" at
  finer grain than the architecture doc anticipated when it assumed the UI layer would have "little to test."

## Explicitly out of scope for this design (deferred, not forgotten)

- **Multi-level nested collections** (`List<List<T>>`) — stays `Unsupported`.
- **Formulas/computed columns** — a TableForge feature not attempted here; Pillar 1's bar is matching/exceeding
  TableForge's *editing* quality, and formulas are a separate, large feature.
- **CSV/JSON import/export** — same reasoning, deferred rather than declared done.
- **Custom rule-authoring UI beyond the default Inspector** — noted as a possible fast-follow if the default
  Inspector proves too clunky in practice; not attempted in this pass.
