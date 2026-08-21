# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

**Scry** is an open-source Unity Editor tool for solo/small-team developers using ScriptableObject-driven ("data as config, not hardcoded logic") game design. It lets you browse, edit, and validate collections of ScriptableObjects as structured data (instead of clicking through assets one at a time in the Inspector), and simulate the emergent balance that data produces (loot rarity distribution, power curves) directly against the real project data — never a re-entered copy that can drift out of sync.

Dogfooding/validation case: *Adventure Dreams* (`../games/unity/Adventure Dreams`), a solo Unity RPG using this exact pattern (6 rarity tiers, 45 creature-specific drop tables, floor/elite/boss scaling). **Adventure Dreams is a validation target, not the spec source** — nothing about Scry may be hardcoded to its specific data shape. See "Design principles" below.

## Status

Design phase complete; implementation not yet started. Full architecture rationale lives in [`docs/superpowers/specs/2026-07-16-architecture-design.md`](docs/superpowers/specs/2026-07-16-architecture-design.md) — read that for the complete picture (problem statement, competitive landscape, data flow, error handling, testing strategy). This file is a shorter orientation pointer, not a duplicate.

## Design principles (non-negotiable — see spec for full rationale)

1. **Generic by construction** — no concept may be hardcoded to Adventure Dreams' vocabulary (rarity tiers, elite/boss multipliers, etc.); everything is configuration.
2. **One real data source** — editing, validation, and simulation all read/write through the same pipeline against the same live project data.
3. **UI-agnostic core, compiler-enforced** — `Core` has zero dependency on any Editor GUI code, enforced by Unity assembly definitions, not convention.
4. **Headless-capable by design** — every core operation must be callable without an interactive Editor window, from v1 onward.
5. **Don't reimplement what must stay true** — anything depending on game *code* (e.g. full combat resolution) is out of scope rather than approximated externally.

## Roadmap

- **v1** — Pillar 1 (data editor: table view, search/filter, bulk-edit, structural validation) + Pillar 2 (loot/economy Monte Carlo simulation against real data).
- **v2** — Pillar 3 (power-curve comparison: player vs. monster power across progression, via a generic configurable formula system).
- **v3** — MCP server interface, exposing the same core library's read/validate/simulate operations to AI coding agents, with dry-run/preview support.
- **Explicitly, indefinitely out of scope** — full turn-by-turn combat simulation.

## Architecture

Four assembly-definition-separated modules (three built for v1/v2):

```
Core            (plain C#, zero UnityEngine/UnityEditor reference — plain-.NET-testable)
  ^
Core.Unity      (Editor-only; bridges Unity <-> Core via AssetDatabase/SerializedObject)
  ^
UI              (UI Toolkit; deliberately thin, no logic of its own)      [v3: Mcp joins here]
```

Technology is C#/.NET throughout, treated as a constraint forced by the domain (Unity's own serialization APIs are required to read/write ScriptableObjects correctly), not a stylistic preference. Target: Unity 6 (6000.x), distributed as a standalone UPM package (this repo is not embedded in any consuming project, including Adventure Dreams).

## Competitive positioning (see spec for full detail)

Odin Inspector (paid, no free full alternative), Machinations.io / Puida's loot designer (simulation disconnected from real data), and existing Unity MCP servers (generic Editor automation, no validation/simulation) all solve pieces of this problem, not the whole thing. One deliberate exception worth knowing up front: **`JoseGomis299/TableForge`** is an actively maintained, feature-rich free tool doing much of what Pillar 1 does (spreadsheet-style editing, formulas, CSV/JSON import/export). The decision was made to keep Pillar 1 at full scope anyway and aim to exceed it, rather than shrink to a minimal data layer — so Pillar 1 work should be held to that bar, not just "good enough to feed Pillar 2."

## Development workflow

`Core` is plain .NET and tested independently of Unity:

```bash
dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj
```

`Core.Unity` requires the Unity Editor. `TestProject/` (checked into this repo) is a throwaway harness that references the package via a `file:` dependency — it exists only to compile and test the package, it is not a product of this tool. Close the Editor before running batch mode (it fails silently if the Editor already has `TestProject` open). Before running batch-mode commands, clean any stray `Package/**/bin` and `Package/**/obj` folders (left behind by `dotnet test`/`dotnet build`), since Unity's asset scanner picks up restored `.dll` files there and breaks `UnityEngine.TestRunner` compilation.

Compilation check:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\compile_check.txt"
```

Success: exit code 0, log ends with `Exiting batchmode successfully now!`. Failure: log contains `Scripts have compiler errors`. Delete `compile_check.txt` after checking.

EditMode test run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Check `test_results.xml` for failures. Delete both scratch files after checking.

Both commands require the Unity Editor to be closed. If it's open: ask the user to close it, or manually review the changed `.cs` files for syntax/type errors before marking a task complete.
