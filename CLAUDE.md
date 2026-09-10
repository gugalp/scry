# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

**Scry** is an open-source Unity Editor tool for solo/small-team developers using ScriptableObject-driven ("data as config, not hardcoded logic") game design. It lets you browse, edit, and validate collections of ScriptableObjects as structured data (instead of clicking through assets one at a time in the Inspector), and simulate the emergent balance that data produces (loot rarity distribution, power curves) directly against the real project data — never a re-entered copy that can drift out of sync.

Dogfooding/validation case: *Adventure Dreams* (`../games/unity/Adventure Dreams`), a solo Unity RPG using this exact pattern (6 rarity tiers, 45 creature-specific drop tables, floor/elite/boss scaling). **Adventure Dreams is a validation target, not the spec source** — nothing about Scry may be hardcoded to its specific data shape. See "Design principles" below.

Current status and version scope: [`docs/ROADMAP.md`](docs/ROADMAP.md) (updated as phases complete — this file
stays stable across sessions, that one doesn't). Full architecture rationale — problem statement, competitive
landscape, data flow, error handling, testing strategy: [`docs/superpowers/specs/2026-07-16-architecture-design.md`](docs/superpowers/specs/2026-07-16-architecture-design.md).

## Design principles (non-negotiable — see spec for full rationale)

1. **Generic by construction** — no concept may be hardcoded to Adventure Dreams' vocabulary (rarity tiers, elite/boss multipliers, etc.); everything is configuration.
2. **One real data source** — editing, validation, and simulation all read/write through the same pipeline against the same live project data.
3. **UI-agnostic core, compiler-enforced** — `Core` has zero dependency on any Editor GUI code, enforced by Unity assembly definitions, not convention.
4. **Headless-capable by design** — every core operation must be callable without an interactive Editor window, from v1 onward.
5. **Don't reimplement what must stay true** — anything depending on game *code* (e.g. full combat resolution) is out of scope rather than approximated externally.
6. **Match or exceed TableForge on Pillar 1** — a free, actively maintained competitor already does much of Pillar 1's spreadsheet-style editing; hold Pillar 1 work to that bar, not just "good enough to feed Pillar 2" (full competitive context in `docs/ROADMAP.md`).

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

On disk, the package itself lives under a `Package/` subfolder (`Package/package.json`, `Package/Runtime/`,
`Package/Editor/`, `Package/Tests/`) — not the repo root, so Unity's asset scanner doesn't pick up `TestProject/`
as package content. `TestProject/` (the throwaway Unity harness described under "Development workflow" below)
stays at the repo root, sibling to `Package/`, not inside it.

## Development workflow

`Core` is plain .NET and tested independently of Unity:

```bash
dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj
```

`Core.Unity` requires the Unity Editor. `TestProject/` (checked into this repo) is a throwaway harness that references the package via a `file:` dependency — it exists only to compile and test the package, it is not a product of this tool. Close the Editor before running batch mode (it fails silently if the Editor already has `TestProject` open). `dotnet test`/`dotnet build` output is redirected to a repo-root `.buildoutput/` folder via `Package/Directory.Build.props`, so it never lands inside `Package/` where Unity's asset scanner would pick it up.

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
