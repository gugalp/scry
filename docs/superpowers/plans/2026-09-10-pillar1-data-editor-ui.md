# Pillar 1 Part B: Data Editor UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the UI Toolkit `DataGridWindow` described in spec Section 4 on top of Part A's completed data layer — an Editor window that browses/edits every `ScryConfig`-tracked `ScriptableObject` collection as a spreadsheet-style grid, including expandable Collection-field sub-tables, bulk-edit, search/filter, inline validation, undo, and write-conflict handling.

**Architecture:** One new Editor-only assembly, `Scry.UI`, consuming `Scry.Core`/`Scry.Core.Unity`. Per the spec's testing philosophy, logic that doesn't need an open `EditorWindow` — cell control creation/binding, row filtering, validation-issue lookup and `RecordId`-shape parsing, rule execution, undo-wrapped edit orchestration — lives in small standalone classes with real EditMode tests (UI Toolkit `VisualElement`s and `Undo` both work fine in headless EditMode tests). `DataGridWindow` itself and its `MultiColumnTreeView` wiring stay manual-smoke-tested, per the architecture doc's stance that Editor GUI automation isn't attempted.

**Tech Stack:** C# / .NET, UI Toolkit (`UnityEngine.UIElements` / `UnityEditor.UIElements`), Unity Test Framework EditMode tests, Unity 6000.3.10f1.

**Spec:** [`docs/superpowers/specs/2026-09-09-pillar1-data-editor-design.md`](../specs/2026-09-09-pillar1-data-editor-design.md), Section 4 ("UI" layer) and the relevant parts of Section 5 (testing strategy for `UI`). Sections 1-3 are Part A, already merged.

## Global Constraints

- `Scry.UI` is Editor-only (`includePlatforms: ["Editor"]`), references `Scry.Core` and `Scry.Core.Unity` — same pattern as `Scry.Core.Unity`'s own asmdef.
- Every write goes through `ScriptableObjectRepository`'s existing methods (`ApplyEdit` x2, `AddCollectionEntry`, `RemoveCollectionEntry`) — the UI never touches `SerializedObject`/`AssetDatabase` directly for a data write, only for `Undo.RegisterCompleteObjectUndo` registration immediately before calling one of those methods.
- Write-back conflicts (`WriteConflictException`) surface as a reload-prompt dialog, never a silent overwrite (existing architecture-doc constraint, now implemented in the UI for the first time).
- Keep the data-to-visual-element mapping logic (cell creation/binding, filtering, issue lookup) in small testable classes, separate from `DataGridWindow`'s `EditorWindow`/GUI code, which stays manual-smoke-test only.
- Every new `.cs` file's Unity-generated `.meta` sidecar must be committed in the same commit — this repo has had this gap twice in Part A; each task below ends with an explicit `git status --short` check for it.
- Close the Unity Editor before any batch-mode command; every manual-smoke-test step in this plan requires the Editor to be **open** instead — never run a batch-mode command and a manual smoke test in the same sitting without closing/reopening appropriately.

---

## File Structure

```
Package/
  Editor/
    Scry.UI/
      Scry.UI.asmdef
      CellBinder.cs                # per-FieldType cell control create/bind, leak-safe rebind
      RowFilter.cs                 # global search + per-column filter predicate
      ValidationIssueLocator.cs    # parses the 3 ValidationIssue.RecordId shapes
      ValidationIssueIndex.cs      # issue lookup by (recordId, fieldName) + full list
      ValidationRunner.cs          # TrackedCollection.Rules -> RuleFactory -> Evaluate
      GridState.cs                 # one open tab's DataCollection + issues, kept in sync on edit
      EditGateway.cs                # Undo.RegisterCompleteObjectUndo + repository.ApplyEdit wrapper
      BulkEditor.cs                 # apply one field/value to N records as one undo group
      GridRow.cs                   # tree-row view model (top-level vs. Collection-field detail row)
      DataGridWindow.cs            # the EditorWindow itself
  Tests/
    Scry.UI.Tests/
      Scry.UI.Tests.asmdef
      Fixtures/
        TestUiItemData.cs          # flat ScriptableObject fixture (no Collection field)
        TestUiDropEntry.cs         # [Serializable] element class
        TestUiMonsterData.cs       # ScriptableObject with a List<TestUiDropEntry> field
      CellBinderTests.cs
      RowFilterTests.cs
      ValidationIssueLocatorTests.cs
      ValidationIssueIndexTests.cs
      ValidationRunnerTests.cs
      EditGatewayTests.cs
      BulkEditorTests.cs
TestProject/
  Assets/
    ScryManualTest/
      ManualDropEntry.cs           # manual-smoke-test fixture (checked in, reused every task below)
      ManualTestMonster.cs
```

---

### Task 1: Scaffold `Scry.UI` / `Scry.UI.Tests` assemblies + `CellBinder`

**Files:**
- Create: `Package/Editor/Scry.UI/Scry.UI.asmdef`
- Create: `Package/Tests/Scry.UI.Tests/Scry.UI.Tests.asmdef`
- Create: `Package/Editor/Scry.UI/CellBinder.cs`
- Test: `Package/Tests/Scry.UI.Tests/CellBinderTests.cs`

**Interfaces:**
- Consumes: `Scry.Core.FieldDescriptor`/`FieldType`/`DataRecord`/`Schema` (Part A).
- Produces: `static class CellBinder { static VisualElement CreateCell(FieldDescriptor field); static void BindCell(VisualElement cell, FieldDescriptor field, DataRecord record, Action<object> onValueChanged); }`. Every later task that renders a grid cell uses this and only this to create/bind cells — it is the single place `FieldType` maps to a UI Toolkit control.

- [ ] **Step 1: Create the two asmdef files**

```json
// Package/Editor/Scry.UI/Scry.UI.asmdef
{
    "name": "Scry.UI",
    "rootNamespace": "Scry.UI",
    "references": [
        "Scry.Core",
        "Scry.Core.Unity"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": []
}
```

```json
// Package/Tests/Scry.UI.Tests/Scry.UI.Tests.asmdef
{
    "name": "Scry.UI.Tests",
    "rootNamespace": "Scry.UI.Tests",
    "references": [
        "Scry.Core",
        "Scry.Core.Unity",
        "Scry.UI",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": []
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// Package/Tests/Scry.UI.Tests/CellBinderTests.cs
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scry.Core;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Scry.UI.Tests
{
    public class CellBinderTests
    {
        [Test]
        public void CreateCell_Numeric_ReturnsFloatField()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);

            var cell = CellBinder.CreateCell(field);

            Assert.IsInstanceOf<FloatField>(cell);
        }

        [Test]
        public void CreateCell_String_ReturnsTextField()
        {
            var field = new FieldDescriptor("itemName", FieldType.String);

            Assert.IsInstanceOf<TextField>(CellBinder.CreateCell(field));
        }

        [Test]
        public void CreateCell_Boolean_ReturnsToggle()
        {
            var field = new FieldDescriptor("isUnique", FieldType.Boolean);

            Assert.IsInstanceOf<Toggle>(CellBinder.CreateCell(field));
        }

        [Test]
        public void CreateCell_Reference_ReturnsObjectField()
        {
            var field = new FieldDescriptor("referencedItem", FieldType.Reference);

            Assert.IsInstanceOf<ObjectField>(CellBinder.CreateCell(field));
        }

        [Test]
        public void CreateCell_Collection_ReturnsLabel()
        {
            var elementSchema = new Schema("Entry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var field = new FieldDescriptor("dropTable", FieldType.Collection, elementSchema);

            Assert.IsInstanceOf<Label>(CellBinder.CreateCell(field));
        }

        [Test]
        public void BindCell_Numeric_SetsInitialValueFromRecord()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);
            var cell = (FloatField)CellBinder.CreateCell(field);
            var record = new DataRecord("r1", new Dictionary<string, object> { ["weight"] = 5f });

            CellBinder.BindCell(cell, field, record, _ => { });

            Assert.AreEqual(5f, cell.value);
        }

        [Test]
        public void BindCell_Numeric_SetsCallbackAsUserData()
        {
            // Unity's UI Toolkit only dispatches ChangeEvents to a control attached to a live
            // panel, which headless -batchmode EditMode tests never provide - so this can't be
            // proven by setting cell.value and observing a callback fire. CellBinder's actual
            // design sidesteps that: CreateCell registers exactly one change-handler for the
            // control's whole lifetime, and BindCell only ever swaps which Action<object> that
            // handler currently delegates to, via userData. So the callback CellBinder will
            // invoke on the next real change is directly and deterministically observable here.
            var field = new FieldDescriptor("weight", FieldType.Numeric);
            var cell = (FloatField)CellBinder.CreateCell(field);
            var record = new DataRecord("r1", new Dictionary<string, object> { ["weight"] = 5f });
            Action<object> callback = v => { };

            CellBinder.BindCell(cell, field, record, callback);

            Assert.AreSame(callback, cell.userData);
        }

        [Test]
        public void BindCell_Numeric_RebindingToADifferentRecord_ReplacesRatherThanStacksTheCallback()
        {
            // MultiColumnTreeView reuses the same VisualElement across virtualized rows, calling
            // BindCell again on every rebind. Because CreateCell registers only ONE handler ever
            // (see CellBinder.Invoke), and BindCell only swaps userData, there is structurally no
            // second handler for a stale callback to leak into - proven here by userData holding
            // exactly the most recently bound callback, not both.
            var field = new FieldDescriptor("weight", FieldType.Numeric);
            var cell = (FloatField)CellBinder.CreateCell(field);
            var record1 = new DataRecord("r1", new Dictionary<string, object> { ["weight"] = 5f });
            var record2 = new DataRecord("r2", new Dictionary<string, object> { ["weight"] = 9f });
            Action<object> firstCallback = v => { };
            Action<object> secondCallback = v => { };

            CellBinder.BindCell(cell, field, record1, firstCallback);
            CellBinder.BindCell(cell, field, record2, secondCallback);

            Assert.AreSame(secondCallback, cell.userData);
            Assert.AreNotSame(firstCallback, cell.userData);
        }

        [Test]
        public void BindCell_Collection_ShowsEntryCount()
        {
            var elementSchema = new Schema("Entry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var field = new FieldDescriptor("dropTable", FieldType.Collection, elementSchema);
            var cell = (Label)CellBinder.CreateCell(field);
            var entries = new List<DataRecord>
            {
                new DataRecord("r1#0", new Dictionary<string, object> { ["itemId"] = "sword" }),
                new DataRecord("r1#1", new Dictionary<string, object> { ["itemId"] = "shield" })
            };
            var record = new DataRecord("r1", new Dictionary<string, object> { ["dropTable"] = (IReadOnlyList<DataRecord>)entries });

            CellBinder.BindCell(cell, field, record, _ => { });

            Assert.AreEqual("2 entries", cell.text);
        }

        [Test]
        public void BindCell_Collection_EmptyList_ShowsZeroEntries()
        {
            var elementSchema = new Schema("Entry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var field = new FieldDescriptor("dropTable", FieldType.Collection, elementSchema);
            var cell = (Label)CellBinder.CreateCell(field);
            var record = new DataRecord("r1", new Dictionary<string, object> { ["dropTable"] = (IReadOnlyList<DataRecord>)new List<DataRecord>() });

            CellBinder.BindCell(cell, field, record, _ => { });

            Assert.AreEqual("0 entries", cell.text);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: compile failure — `CellBinder` doesn't exist yet.

- [ ] **Step 4: Implement `CellBinder`**

```csharp
// Package/Editor/Scry.UI/CellBinder.cs
using System;
using Scry.Core;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Scry.UI
{
    public static class CellBinder
    {
        public static VisualElement CreateCell(FieldDescriptor field)
        {
            switch (field.Type)
            {
                case FieldType.Numeric:
                    var floatField = new FloatField { isDelayed = true };
                    floatField.RegisterValueChangedCallback(evt => Invoke(floatField, evt.newValue));
                    return floatField;
                case FieldType.String:
                    var textField = new TextField { isDelayed = true };
                    textField.RegisterValueChangedCallback(evt => Invoke(textField, evt.newValue));
                    return textField;
                case FieldType.Boolean:
                    var toggle = new Toggle();
                    toggle.RegisterValueChangedCallback(evt => Invoke(toggle, evt.newValue));
                    return toggle;
                case FieldType.Enum:
                    var intField = new IntegerField { isDelayed = true };
                    intField.RegisterValueChangedCallback(evt => Invoke(intField, evt.newValue));
                    return intField;
                case FieldType.Reference:
                    var objectField = new ObjectField();
                    objectField.RegisterValueChangedCallback(evt => Invoke(objectField, evt.newValue));
                    return objectField;
                default:
                    return new Label();
            }
        }

        public static void BindCell(VisualElement cell, FieldDescriptor field, DataRecord record, Action<object> onValueChanged)
        {
            var value = record.GetValue(field.Name);

            switch (field.Type)
            {
                case FieldType.Numeric:
                    var floatField = (FloatField)cell;
                    floatField.SetValueWithoutNotify(Convert.ToSingle(value ?? 0f));
                    floatField.userData = onValueChanged;
                    break;
                case FieldType.String:
                    var textField = (TextField)cell;
                    textField.SetValueWithoutNotify((string)value ?? string.Empty);
                    textField.userData = onValueChanged;
                    break;
                case FieldType.Boolean:
                    var toggle = (Toggle)cell;
                    toggle.SetValueWithoutNotify(value is bool b && b);
                    toggle.userData = onValueChanged;
                    break;
                case FieldType.Enum:
                    var intField = (IntegerField)cell;
                    intField.SetValueWithoutNotify(Convert.ToInt32(value ?? 0));
                    intField.userData = onValueChanged;
                    break;
                case FieldType.Reference:
                    var objectField = (ObjectField)cell;
                    objectField.SetValueWithoutNotify(value as UnityEngine.Object);
                    objectField.userData = onValueChanged;
                    break;
                case FieldType.Collection:
                    var entries = value as System.Collections.Generic.IReadOnlyList<DataRecord>;
                    ((Label)cell).text = $"{entries?.Count ?? 0} entries";
                    break;
                default:
                    ((Label)cell).text = "(unsupported)";
                    break;
            }
        }

        // The change-callback is registered exactly once, when the control is created
        // (CreateCell) - not on every BindCell call, which happens repeatedly on the SAME
        // element as MultiColumnTreeView reuses it across virtualized rows. BindCell only ever
        // swaps which Action<object> is currently stored in userData, so a rebind never touches
        // Unity's event system and there is never more than one registered handler to begin
        // with - "no stacking" is provable by inspecting userData directly (see
        // CellBinderTests), without needing a live UI Toolkit panel to observe event dispatch
        // (unavailable in headless batch-mode EditMode tests).
        private static void Invoke(VisualElement field, object newValue)
        {
            if (field.userData is Action<object> callback)
                callback(newValue);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 3.
Expected: exit code 0; `test_results.xml` shows all 9 `CellBinderTests` passing. Delete `test_results.xml` and `test_run.txt` after checking.

- [ ] **Step 6: Confirm `.meta` files and commit**

```bash
git status --short
```

Confirm `Scry.UI.asmdef`, `Scry.UI.Tests.asmdef`, `CellBinder.cs`, and `CellBinderTests.cs` all have matching `.meta` files listed, then:

```bash
git add Package/Editor/Scry.UI/Scry.UI.asmdef Package/Editor/Scry.UI/Scry.UI.asmdef.meta Package/Editor/Scry.UI/CellBinder.cs Package/Editor/Scry.UI/CellBinder.cs.meta Package/Tests/Scry.UI.Tests/Scry.UI.Tests.asmdef Package/Tests/Scry.UI.Tests/Scry.UI.Tests.asmdef.meta Package/Tests/Scry.UI.Tests/CellBinderTests.cs Package/Tests/Scry.UI.Tests/CellBinderTests.cs.meta
git commit -m "feat(ui): scaffold Scry.UI assembly and add CellBinder"
```

---

### Task 2: `RowFilter`

**Files:**
- Create: `Package/Editor/Scry.UI/RowFilter.cs`
- Test: `Package/Tests/Scry.UI.Tests/RowFilterTests.cs`

**Interfaces:**
- Consumes: `Scry.Core.DataRecord`/`Schema`/`FieldType` (Part A).
- Produces: `enum ColumnFilterMode { Contains, Range, EnumValues, BooleanState }`; `class ColumnFilter { string FieldName; ColumnFilterMode Mode; string TextContains; double? Min; double? Max; HashSet<int> AllowedEnumValues; bool? BooleanValue; }`; `static class RowFilter { static bool Matches(DataRecord record, Schema schema, string searchText, IReadOnlyList<ColumnFilter> columnFilters); }`. Consumed by the search/filter toolbar (Task 10).

- [ ] **Step 1: Write the failing tests**

```csharp
// Package/Tests/Scry.UI.Tests/RowFilterTests.cs
using System.Collections.Generic;
using NUnit.Framework;
using Scry.Core;

namespace Scry.UI.Tests
{
    public class RowFilterTests
    {
        private static readonly Schema TestSchema = new Schema("Item", new[]
        {
            new FieldDescriptor("itemName", FieldType.String),
            new FieldDescriptor("weight", FieldType.Numeric),
            new FieldDescriptor("rarity", FieldType.Enum),
            new FieldDescriptor("isUnique", FieldType.Boolean)
        });

        private static DataRecord BuildRecord(string itemName, double weight, int rarity, bool isUnique)
        {
            return new DataRecord("r1", new Dictionary<string, object>
            {
                ["itemName"] = itemName,
                ["weight"] = weight,
                ["rarity"] = rarity,
                ["isUnique"] = isUnique
            });
        }

        [Test]
        public void Matches_NoSearchTextNoFilters_ReturnsTrue()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);

            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, null));
        }

        [Test]
        public void Matches_SearchText_MatchesAnyFieldCaseInsensitively()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);

            Assert.IsTrue(RowFilter.Matches(record, TestSchema, "rusty", null));
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, "shield", null));
        }

        [Test]
        public void Matches_ContainsColumnFilter_FiltersOnThatField()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);
            var filters = new[] { new ColumnFilter("itemName", ColumnFilterMode.Contains) { TextContains = "sword" } };

            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, filters));

            filters = new[] { new ColumnFilter("itemName", ColumnFilterMode.Contains) { TextContains = "shield" } };
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, null, filters));
        }

        [Test]
        public void Matches_RangeColumnFilter_RespectsMinAndMax()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);

            var withinRange = new[] { new ColumnFilter("weight", ColumnFilterMode.Range) { Min = 1, Max = 10 } };
            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, withinRange));

            var outsideRange = new[] { new ColumnFilter("weight", ColumnFilterMode.Range) { Min = 6 } };
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, null, outsideRange));
        }

        [Test]
        public void Matches_EnumValuesColumnFilter_RestrictsToAllowedSet()
        {
            var record = BuildRecord("Rusty Sword", 5, 2, false);

            var allowed = new[] { new ColumnFilter("rarity", ColumnFilterMode.EnumValues) { AllowedEnumValues = new HashSet<int> { 2, 3 } } };
            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, allowed));

            var disallowed = new[] { new ColumnFilter("rarity", ColumnFilterMode.EnumValues) { AllowedEnumValues = new HashSet<int> { 0, 1 } } };
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, null, disallowed));
        }

        [Test]
        public void Matches_BooleanStateColumnFilter_RestrictsToThatState()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, true);

            var wantTrue = new[] { new ColumnFilter("isUnique", ColumnFilterMode.BooleanState) { BooleanValue = true } };
            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, wantTrue));

            var wantFalse = new[] { new ColumnFilter("isUnique", ColumnFilterMode.BooleanState) { BooleanValue = false } };
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, null, wantFalse));
        }

        [Test]
        public void Matches_SearchTextAndColumnFilter_BothMustPass()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);
            var filters = new[] { new ColumnFilter("weight", ColumnFilterMode.Range) { Min = 10 } };

            Assert.IsFalse(RowFilter.Matches(record, TestSchema, "rusty", filters));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run the batch-mode EditMode test command (same as Task 1 Step 3).
Expected: compile failure — `RowFilter`/`ColumnFilter`/`ColumnFilterMode` don't exist yet.

- [ ] **Step 3: Implement `RowFilter`**

```csharp
// Package/Editor/Scry.UI/RowFilter.cs
using System;
using System.Collections.Generic;
using Scry.Core;

namespace Scry.UI
{
    public enum ColumnFilterMode
    {
        Contains,
        Range,
        EnumValues,
        BooleanState
    }

    public sealed class ColumnFilter
    {
        public string FieldName { get; }
        public ColumnFilterMode Mode { get; }
        public string TextContains { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public HashSet<int> AllowedEnumValues { get; set; }
        public bool? BooleanValue { get; set; }

        public ColumnFilter(string fieldName, ColumnFilterMode mode)
        {
            FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
            Mode = mode;
        }
    }

    public static class RowFilter
    {
        public static bool Matches(DataRecord record, Schema schema, string searchText, IReadOnlyList<ColumnFilter> columnFilters)
        {
            if (!MatchesSearchText(record, schema, searchText))
                return false;

            if (columnFilters == null)
                return true;

            foreach (var filter in columnFilters)
            {
                if (!MatchesColumnFilter(record, filter))
                    return false;
            }

            return true;
        }

        private static bool MatchesSearchText(DataRecord record, Schema schema, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return true;

            foreach (var field in schema.Fields)
            {
                if (field.Type == FieldType.Collection)
                    continue;

                var value = record.GetValue(field.Name);
                if (value != null && value.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool MatchesColumnFilter(DataRecord record, ColumnFilter filter)
        {
            var value = record.GetValue(filter.FieldName);

            switch (filter.Mode)
            {
                case ColumnFilterMode.Contains:
                    return string.IsNullOrEmpty(filter.TextContains)
                        || (value != null && value.ToString().IndexOf(filter.TextContains, StringComparison.OrdinalIgnoreCase) >= 0);
                case ColumnFilterMode.Range:
                    var numeric = value == null ? 0 : Convert.ToDouble(value);
                    if (filter.Min.HasValue && numeric < filter.Min.Value)
                        return false;
                    if (filter.Max.HasValue && numeric > filter.Max.Value)
                        return false;
                    return true;
                case ColumnFilterMode.EnumValues:
                    if (filter.AllowedEnumValues == null || filter.AllowedEnumValues.Count == 0)
                        return true;
                    return filter.AllowedEnumValues.Contains(Convert.ToInt32(value ?? 0));
                case ColumnFilterMode.BooleanState:
                    if (!filter.BooleanValue.HasValue)
                        return true;
                    return (value is bool b && b) == filter.BooleanValue.Value;
                default:
                    return true;
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch-mode test command. Expected: all 7 `RowFilterTests` pass. Delete scratch logs.

- [ ] **Step 5: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/RowFilter.cs Package/Editor/Scry.UI/RowFilter.cs.meta Package/Tests/Scry.UI.Tests/RowFilterTests.cs Package/Tests/Scry.UI.Tests/RowFilterTests.cs.meta
git commit -m "feat(ui): add RowFilter for global search and per-column filters"
```

---

### Task 3: `ValidationIssueLocator`

**Files:**
- Create: `Package/Editor/Scry.UI/ValidationIssueLocator.cs`
- Test: `Package/Tests/Scry.UI.Tests/ValidationIssueLocatorTests.cs`

**Interfaces:**
- Consumes: `Scry.Core.ValidationIssue`. Documents (and parses) the three `RecordId` shapes established in Part A: `null`/`"*"` (collection-wide), `"{parentId}"` (top-level or nested-issue parent), `"{parentId}/{groupKey}"` (nested + grouped), `"{parentId}#{index}"` (one Collection-field entry).
- Produces: `readonly struct IssueLocation { bool IsCollectionWide; string ParentRecordId; string NestedFieldGroupKey; int? ChildIndex; }`; `static class ValidationIssueLocator { static IssueLocation Locate(ValidationIssue issue); }`. Consumed by `ValidationIssueIndex` (Task 4) and the issues panel's click-to-navigate (Task 11).

- [ ] **Step 1: Write the failing tests**

```csharp
// Package/Tests/Scry.UI.Tests/ValidationIssueLocatorTests.cs
using NUnit.Framework;
using Scry.Core;

namespace Scry.UI.Tests
{
    public class ValidationIssueLocatorTests
    {
        [Test]
        public void Locate_NullRecordId_IsCollectionWide()
        {
            var issue = new ValidationIssue(null, "weight", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.IsTrue(location.IsCollectionWide);
            Assert.IsNull(location.ParentRecordId);
        }

        [Test]
        public void Locate_UngroupedSentinel_IsCollectionWide()
        {
            var issue = new ValidationIssue("*", "weight", "message");

            Assert.IsTrue(ValidationIssueLocator.Locate(issue).IsCollectionWide);
        }

        [Test]
        public void Locate_PlainGuid_IsParentRecordOnly()
        {
            var issue = new ValidationIssue("abc123", "weight", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.IsFalse(location.IsCollectionWide);
            Assert.AreEqual("abc123", location.ParentRecordId);
            Assert.IsNull(location.NestedFieldGroupKey);
            Assert.IsNull(location.ChildIndex);
        }

        [Test]
        public void Locate_GroupedNestedId_ParsesParentAndGroupKey()
        {
            var issue = new ValidationIssue("abc123/goblin", "weight", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.IsFalse(location.IsCollectionWide);
            Assert.AreEqual("abc123", location.ParentRecordId);
            Assert.AreEqual("goblin", location.NestedFieldGroupKey);
            Assert.IsNull(location.ChildIndex);
        }

        [Test]
        public void Locate_ChildEntryId_ParsesParentAndIndex()
        {
            var issue = new ValidationIssue("abc123#2", "itemId", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.IsFalse(location.IsCollectionWide);
            Assert.AreEqual("abc123", location.ParentRecordId);
            Assert.AreEqual(2, location.ChildIndex);
            Assert.IsNull(location.NestedFieldGroupKey);
        }

        [Test]
        public void Locate_MalformedIndexSuffix_FallsBackToTreatingWholeIdAsParent()
        {
            var issue = new ValidationIssue("abc123#notanumber", "itemId", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.AreEqual("abc123#notanumber", location.ParentRecordId);
            Assert.IsNull(location.ChildIndex);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run the batch-mode EditMode test command.
Expected: compile failure — `ValidationIssueLocator`/`IssueLocation` don't exist yet.

- [ ] **Step 3: Implement `ValidationIssueLocator`**

```csharp
// Package/Editor/Scry.UI/ValidationIssueLocator.cs
using Scry.Core;

namespace Scry.UI
{
    public readonly struct IssueLocation
    {
        public bool IsCollectionWide { get; }
        public string ParentRecordId { get; }
        public string NestedFieldGroupKey { get; }
        public int? ChildIndex { get; }

        public IssueLocation(bool isCollectionWide, string parentRecordId, string nestedFieldGroupKey, int? childIndex)
        {
            IsCollectionWide = isCollectionWide;
            ParentRecordId = parentRecordId;
            NestedFieldGroupKey = nestedFieldGroupKey;
            ChildIndex = childIndex;
        }
    }

    // Interprets the RecordId shapes ValidationIssue can carry. Scry.Core.Rules never formalizes
    // this as a type - it's purely a UI-side concern for deciding which row to highlight and
    // whether to expand a parent's Collection field first:
    //   null or "*"          - collection-wide, not tied to any row
    //   "{parentId}"          - a top-level row, or the parent of a nested-field issue
    //   "{parentId}/{key}"    - a nested-field issue grouped by a sub-field (SumEqualsRule + groupByField)
    //   "{parentId}#{index}"  - one specific entry inside a Collection field (NoDuplicateRule)
    public static class ValidationIssueLocator
    {
        private const string UngroupedKey = "*";

        public static IssueLocation Locate(ValidationIssue issue)
        {
            var recordId = issue.RecordId;

            if (string.IsNullOrEmpty(recordId) || recordId == UngroupedKey)
                return new IssueLocation(isCollectionWide: true, parentRecordId: null, nestedFieldGroupKey: null, childIndex: null);

            var groupSeparatorIndex = recordId.IndexOf('/');
            if (groupSeparatorIndex >= 0)
            {
                var parentId = recordId.Substring(0, groupSeparatorIndex);
                var groupKey = recordId.Substring(groupSeparatorIndex + 1);
                return new IssueLocation(isCollectionWide: false, parentRecordId: parentId, nestedFieldGroupKey: groupKey, childIndex: null);
            }

            var indexSeparatorIndex = recordId.IndexOf('#');
            if (indexSeparatorIndex >= 0)
            {
                var parentId = recordId.Substring(0, indexSeparatorIndex);
                var indexText = recordId.Substring(indexSeparatorIndex + 1);
                if (int.TryParse(indexText, out var index))
                    return new IssueLocation(isCollectionWide: false, parentRecordId: parentId, nestedFieldGroupKey: null, childIndex: index);

                return new IssueLocation(isCollectionWide: false, parentRecordId: recordId, nestedFieldGroupKey: null, childIndex: null);
            }

            return new IssueLocation(isCollectionWide: false, parentRecordId: recordId, nestedFieldGroupKey: null, childIndex: null);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch-mode test command. Expected: all 6 `ValidationIssueLocatorTests` pass. Delete scratch logs.

- [ ] **Step 5: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/ValidationIssueLocator.cs Package/Editor/Scry.UI/ValidationIssueLocator.cs.meta Package/Tests/Scry.UI.Tests/ValidationIssueLocatorTests.cs Package/Tests/Scry.UI.Tests/ValidationIssueLocatorTests.cs.meta
git commit -m "feat(ui): add ValidationIssueLocator to parse RecordId shapes"
```

---

### Task 4: `ValidationIssueIndex`

**Files:**
- Create: `Package/Editor/Scry.UI/ValidationIssueIndex.cs`
- Test: `Package/Tests/Scry.UI.Tests/ValidationIssueIndexTests.cs`

**Interfaces:**
- Consumes: `Scry.Core.ValidationIssue`/`ValidationSeverity`, `ValidationIssueLocator.Locate` (Task 3).
- Produces: `sealed class ValidationIssueIndex { ValidationIssueIndex(IReadOnlyList<ValidationIssue> issues); IReadOnlyList<ValidationIssue> AllIssues; IReadOnlyList<ValidationIssue> IssuesFor(string recordId, string fieldName); ValidationSeverity? HighestSeverityFor(string recordId, string fieldName); }`. Consumed by `GridState` (Task 5) and cell-decoration/issues-panel code (Task 11).

- [ ] **Step 1: Write the failing tests**

```csharp
// Package/Tests/Scry.UI.Tests/ValidationIssueIndexTests.cs
using System.Collections.Generic;
using NUnit.Framework;
using Scry.Core;

namespace Scry.UI.Tests
{
    public class ValidationIssueIndexTests
    {
        [Test]
        public void IssuesFor_ReturnsIssuesMatchingRecordAndField()
        {
            var issues = new List<ValidationIssue>
            {
                new ValidationIssue("r1", "weight", "too heavy"),
                new ValidationIssue("r1", "itemName", "duplicate name"),
                new ValidationIssue("r2", "weight", "too heavy")
            };
            var index = new ValidationIssueIndex(issues);

            var result = index.IssuesFor("r1", "weight");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("too heavy", result[0].Message);
        }

        [Test]
        public void IssuesFor_NoMatch_ReturnsEmpty()
        {
            var index = new ValidationIssueIndex(new List<ValidationIssue>());

            Assert.IsEmpty(index.IssuesFor("r1", "weight"));
        }

        [Test]
        public void HighestSeverityFor_NoIssues_ReturnsNull()
        {
            var index = new ValidationIssueIndex(new List<ValidationIssue>());

            Assert.IsNull(index.HighestSeverityFor("r1", "weight"));
        }

        [Test]
        public void HighestSeverityFor_PrefersErrorOverWarning()
        {
            var issues = new List<ValidationIssue>
            {
                new ValidationIssue("r1", "weight", "warn", ValidationSeverity.Warning),
                new ValidationIssue("r1", "weight", "err", ValidationSeverity.Error)
            };
            var index = new ValidationIssueIndex(issues);

            Assert.AreEqual(ValidationSeverity.Error, index.HighestSeverityFor("r1", "weight"));
        }

        [Test]
        public void IssuesFor_NestedChildEntryId_MatchesByParsedParentId()
        {
            // NoDuplicateRule emits the CHILD's own id ("r1#1"), not the parent's - IssuesFor
            // should still be queryable by the parent record id the UI actually has on hand for
            // a top-level row's decoration, via the locator's parsed ParentRecordId.
            var issues = new List<ValidationIssue> { new ValidationIssue("r1#1", "itemId", "duplicate") };
            var index = new ValidationIssueIndex(issues);

            Assert.AreEqual(1, index.IssuesFor("r1", "itemId").Count);
        }

        [Test]
        public void AllIssues_ReturnsEveryIssuePassedIn()
        {
            var issues = new List<ValidationIssue>
            {
                new ValidationIssue("r1", "weight", "a"),
                new ValidationIssue("r2", "weight", "b")
            };

            var index = new ValidationIssueIndex(issues);

            Assert.AreEqual(2, index.AllIssues.Count);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run the batch-mode EditMode test command.
Expected: compile failure — `ValidationIssueIndex` doesn't exist yet.

- [ ] **Step 3: Implement `ValidationIssueIndex`**

```csharp
// Package/Editor/Scry.UI/ValidationIssueIndex.cs
using System.Collections.Generic;
using System.Linq;
using Scry.Core;

namespace Scry.UI
{
    public sealed class ValidationIssueIndex
    {
        private readonly ILookup<(string RecordId, string FieldName), ValidationIssue> _byCell;

        public IReadOnlyList<ValidationIssue> AllIssues { get; }

        public ValidationIssueIndex(IReadOnlyList<ValidationIssue> issues)
        {
            AllIssues = issues;
            _byCell = issues.ToLookup(i => (ValidationIssueLocator.Locate(i).ParentRecordId ?? string.Empty, i.FieldName));
        }

        public IReadOnlyList<ValidationIssue> IssuesFor(string recordId, string fieldName)
        {
            return _byCell[(recordId ?? string.Empty, fieldName)].ToList();
        }

        public ValidationSeverity? HighestSeverityFor(string recordId, string fieldName)
        {
            var issues = IssuesFor(recordId, fieldName);
            if (issues.Count == 0)
                return null;

            return issues.Any(i => i.Severity == ValidationSeverity.Error) ? ValidationSeverity.Error : ValidationSeverity.Warning;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch-mode test command. Expected: all 6 `ValidationIssueIndexTests` pass. Delete scratch logs.

- [ ] **Step 5: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/ValidationIssueIndex.cs Package/Editor/Scry.UI/ValidationIssueIndex.cs.meta Package/Tests/Scry.UI.Tests/ValidationIssueIndexTests.cs Package/Tests/Scry.UI.Tests/ValidationIssueIndexTests.cs.meta
git commit -m "feat(ui): add ValidationIssueIndex for per-cell issue lookup"
```

---

### Task 5: `ValidationRunner` and `GridState`

**Files:**
- Create: `Package/Editor/Scry.UI/ValidationRunner.cs`
- Create: `Package/Editor/Scry.UI/GridState.cs`
- Test: `Package/Tests/Scry.UI.Tests/ValidationRunnerTests.cs`

**Interfaces:**
- Consumes: `Scry.Core.Unity.RuleFactory.Build` and `Scry.Core.Unity.Config.RuleConfig` (Part A), `Scry.Core.DataCollection`/`DataRecord`, `ValidationIssueIndex` (Task 4).
- Produces: `static class ValidationRunner { static IReadOnlyList<ValidationIssue> Run(DataCollection collection, IReadOnlyList<RuleConfig> ruleConfigs); }` — the first real consumer of `RuleFactory`/`TrackedCollection.Rules` outside tests. `sealed class GridState { GridState(Type scriptableObjectType, DataCollection collection, IReadOnlyList<RuleConfig> rules); Type ScriptableObjectType; DataCollection Collection; IReadOnlyList<RuleConfig> Rules; ValidationIssueIndex Issues; void ReplaceRecord(DataRecord updated); }` — holds one open tab's live state, kept current after every edit. Consumed by `DataGridWindow` from Task 6 onward.

- [ ] **Step 1: Write the failing tests**

```csharp
// Package/Tests/Scry.UI.Tests/ValidationRunnerTests.cs
using System.Collections.Generic;
using NUnit.Framework;
using Scry.Core;
using Scry.Core.Unity.Config;

namespace Scry.UI.Tests
{
    public class ValidationRunnerTests
    {
        private static DataCollection BuildCollection(params (string id, double weight)[] rows)
        {
            var schema = new Schema("Item", new[] { new FieldDescriptor("weight", FieldType.Numeric) });
            var records = new List<DataRecord>();
            foreach (var row in rows)
                records.Add(new DataRecord(row.id, new Dictionary<string, object> { ["weight"] = row.weight }));
            return new DataCollection(schema, records);
        }

        [Test]
        public void Run_NoRules_ReturnsEmpty()
        {
            var collection = BuildCollection(("r1", 100));

            var issues = ValidationRunner.Run(collection, new List<RuleConfig>());

            Assert.IsEmpty(issues);
        }

        [Test]
        public void Run_SumEqualsRuleConfig_ReportsIssue_WhenSumDoesNotMatch()
        {
            var collection = BuildCollection(("r1", 60), ("r2", 30));
            var rules = new List<RuleConfig> { new SumEqualsRuleConfig { Field = "weight", Target = 100 } };

            var issues = ValidationRunner.Run(collection, rules);

            Assert.AreEqual(1, issues.Count);
        }

        [Test]
        public void Run_SumEqualsRuleConfig_NoIssue_WhenSumMatches()
        {
            var collection = BuildCollection(("r1", 60), ("r2", 40));
            var rules = new List<RuleConfig> { new SumEqualsRuleConfig { Field = "weight", Target = 100 } };

            Assert.IsEmpty(ValidationRunner.Run(collection, rules));
        }

        [Test]
        public void Run_MultipleRuleConfigs_CombinesIssuesFromAll()
        {
            var collection = BuildCollection(("r1", 60));
            var rules = new List<RuleConfig>
            {
                new SumEqualsRuleConfig { Field = "weight", Target = 100 },
                new RequiredAtLeastOnceRuleConfig { Field = "weight", RequiredValue = "999" }
            };

            var issues = ValidationRunner.Run(collection, rules);

            Assert.AreEqual(2, issues.Count);
        }

        [Test]
        public void GridState_ReplaceRecord_UpdatesCollectionAndRecomputesIssues()
        {
            var collection = BuildCollection(("r1", 60), ("r2", 30));
            var rules = new List<RuleConfig> { new SumEqualsRuleConfig { Field = "weight", Target = 100 } };
            var state = new GridState(typeof(object), collection, rules);

            Assert.AreEqual(1, state.Issues.AllIssues.Count);

            var updated = new DataRecord("r2", new Dictionary<string, object> { ["weight"] = 40.0 }, "fingerprint");
            state.ReplaceRecord(updated);

            Assert.AreEqual(40.0, state.Collection.Records[1].GetValue("weight"));
            Assert.IsEmpty(state.Issues.AllIssues);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run the batch-mode EditMode test command.
Expected: compile failure — `ValidationRunner`/`GridState` don't exist yet.

- [ ] **Step 3: Implement `ValidationRunner` and `GridState`**

```csharp
// Package/Editor/Scry.UI/ValidationRunner.cs
using System.Collections.Generic;
using Scry.Core;
using Scry.Core.Unity;
using Scry.Core.Unity.Config;

namespace Scry.UI
{
    public static class ValidationRunner
    {
        public static IReadOnlyList<ValidationIssue> Run(DataCollection collection, IReadOnlyList<RuleConfig> ruleConfigs)
        {
            var issues = new List<ValidationIssue>();

            foreach (var ruleConfig in ruleConfigs)
            {
                var rule = RuleFactory.Build(ruleConfig);
                issues.AddRange(rule.Evaluate(collection));
            }

            return issues;
        }
    }
}
```

```csharp
// Package/Editor/Scry.UI/GridState.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Scry.Core;
using Scry.Core.Unity.Config;

namespace Scry.UI
{
    // Holds one open DataGridWindow tab's live data: the scanned DataCollection and the
    // ValidationIssues it currently produces. ReplaceRecord is how an edit propagates - it swaps
    // one record in the collection and re-runs validation, so the grid always reflects the asset
    // state actually on disk after a write.
    public sealed class GridState
    {
        public Type ScriptableObjectType { get; }
        public IReadOnlyList<RuleConfig> Rules { get; }
        public DataCollection Collection { get; private set; }
        public ValidationIssueIndex Issues { get; private set; }

        public GridState(Type scriptableObjectType, DataCollection collection, IReadOnlyList<RuleConfig> rules)
        {
            ScriptableObjectType = scriptableObjectType;
            Rules = rules;
            Collection = collection;
            Issues = new ValidationIssueIndex(ValidationRunner.Run(collection, rules));
        }

        public void ReplaceRecord(DataRecord updated)
        {
            var records = Collection.Records.Select(r => r.Id == updated.Id ? updated : r).ToList();
            Collection = new DataCollection(Collection.Schema, records);
            Issues = new ValidationIssueIndex(ValidationRunner.Run(Collection, Rules));
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch-mode test command. Expected: all 5 `ValidationRunnerTests` pass. Delete scratch logs.

- [ ] **Step 5: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/ValidationRunner.cs Package/Editor/Scry.UI/ValidationRunner.cs.meta Package/Editor/Scry.UI/GridState.cs Package/Editor/Scry.UI/GridState.cs.meta Package/Tests/Scry.UI.Tests/ValidationRunnerTests.cs Package/Tests/Scry.UI.Tests/ValidationRunnerTests.cs.meta
git commit -m "feat(ui): add ValidationRunner and GridState"
```

---

### Task 6: `EditGateway` and `BulkEditor`

**Files:**
- Create: `Package/Editor/Scry.UI/EditGateway.cs`
- Create: `Package/Editor/Scry.UI/BulkEditor.cs`
- Create: `Package/Tests/Scry.UI.Tests/Fixtures/TestUiItemData.cs`
- Test: `Package/Tests/Scry.UI.Tests/EditGatewayTests.cs`
- Test: `Package/Tests/Scry.UI.Tests/BulkEditorTests.cs`

**Interfaces:**
- Consumes: `Scry.Core.Unity.ScriptableObjectRepository.ApplyEdit` (both overloads, Part A).
- Produces: `static class EditGateway { static DataRecord ApplyEdit(ScriptableObjectRepository repository, DataRecord record, string fieldName, object value, Type scriptableObjectType, string undoName); static DataRecord ApplyNestedEdit(ScriptableObjectRepository repository, DataRecord record, string collectionField, int index, string childFieldName, object value, Type scriptableObjectType, string undoName); }` and `static class BulkEditor { static void ApplyToSelected(ScriptableObjectRepository repository, IEnumerable<DataRecord> selectedRecords, string fieldName, object value, Type scriptableObjectType, Action<DataRecord> onRecordUpdated); }`. Every write path in `DataGridWindow` (Tasks 8-9) goes through one of these two, never the repository directly, so every edit is undo-registered consistently.

- [ ] **Step 1: Create the fixture**

```csharp
// Package/Tests/Scry.UI.Tests/Fixtures/TestUiItemData.cs
using UnityEngine;

namespace Scry.UI.Tests.Fixtures
{
    public class TestUiItemData : ScriptableObject
    {
        public string itemName;
        public float weight;
    }
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// Package/Tests/Scry.UI.Tests/EditGatewayTests.cs
using NUnit.Framework;
using Scry.Core.Unity;
using Scry.UI.Tests.Fixtures;
using UnityEditor;
using UnityEngine;

namespace Scry.UI.Tests
{
    public class EditGatewayTests
    {
        private const string FixtureFolder = "Assets/ScryUiTestFixtures";
        private ScriptableObjectRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _repository = new ScriptableObjectRepository();
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
                AssetDatabase.CreateFolder("Assets", "ScryUiTestFixtures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }

        [Test]
        public void ApplyEdit_WritesValueBackToAsset()
        {
            var item = ScriptableObject.CreateInstance<TestUiItemData>();
            item.itemName = "Rusty Sword";
            AssetDatabase.CreateAsset(item, $"{FixtureFolder}/RustySword.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestUiItemData));
            var record = collection.Records[0];

            var updated = EditGateway.ApplyEdit(_repository, record, "itemName", "Legendary Sword", typeof(TestUiItemData), "Edit itemName");

            Assert.AreEqual("Legendary Sword", updated.GetValue("itemName"));
        }

        [Test]
        public void ApplyEdit_RegistersUndo_SoRevertingTheGroupRestoresThePreviousValue()
        {
            var item = ScriptableObject.CreateInstance<TestUiItemData>();
            item.itemName = "Rusty Sword";
            AssetDatabase.CreateAsset(item, $"{FixtureFolder}/RustySword.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestUiItemData));
            var record = collection.Records[0];

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            EditGateway.ApplyEdit(_repository, record, "itemName", "Legendary Sword", typeof(TestUiItemData), "Edit itemName");

            var afterEdit = _repository.Scan(typeof(TestUiItemData));
            Assert.AreEqual("Legendary Sword", afterEdit.Records[0].GetValue("itemName"));

            Undo.RevertAllDownToGroup(undoGroup);

            var afterUndo = _repository.Scan(typeof(TestUiItemData));
            Assert.AreEqual("Rusty Sword", afterUndo.Records[0].GetValue("itemName"));
        }
    }
}
```

```csharp
// Package/Tests/Scry.UI.Tests/BulkEditorTests.cs
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scry.Core;
using Scry.Core.Unity;
using Scry.UI.Tests.Fixtures;
using UnityEditor;
using UnityEngine;

namespace Scry.UI.Tests
{
    public class BulkEditorTests
    {
        private const string FixtureFolder = "Assets/ScryUiTestFixtures";
        private ScriptableObjectRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _repository = new ScriptableObjectRepository();
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
                AssetDatabase.CreateFolder("Assets", "ScryUiTestFixtures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }

        [Test]
        public void ApplyToSelected_WritesValueToEveryRecord()
        {
            var itemA = ScriptableObject.CreateInstance<TestUiItemData>();
            itemA.itemName = "A";
            AssetDatabase.CreateAsset(itemA, $"{FixtureFolder}/A.asset");
            var itemB = ScriptableObject.CreateInstance<TestUiItemData>();
            itemB.itemName = "B";
            AssetDatabase.CreateAsset(itemB, $"{FixtureFolder}/B.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestUiItemData));
            var updatedRecords = new List<DataRecord>();

            BulkEditor.ApplyToSelected(_repository, collection.Records, "weight", 9f, typeof(TestUiItemData), r => updatedRecords.Add(r));

            Assert.AreEqual(2, updatedRecords.Count);
            Assert.IsTrue(updatedRecords.All(r => (float)r.GetValue("weight") == 9f));
        }

        [Test]
        public void ApplyToSelected_CollapsesAllEditsIntoOneUndoGroup()
        {
            var itemA = ScriptableObject.CreateInstance<TestUiItemData>();
            itemA.itemName = "A";
            AssetDatabase.CreateAsset(itemA, $"{FixtureFolder}/A.asset");
            var itemB = ScriptableObject.CreateInstance<TestUiItemData>();
            itemB.itemName = "B";
            AssetDatabase.CreateAsset(itemB, $"{FixtureFolder}/B.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestUiItemData));

            Undo.IncrementCurrentGroup();
            var groupBeforeBulkEdit = Undo.GetCurrentGroup();

            BulkEditor.ApplyToSelected(_repository, collection.Records, "weight", 9f, typeof(TestUiItemData), _ => { });

            var afterBulkEdit = _repository.Scan(typeof(TestUiItemData));
            Assert.IsTrue(afterBulkEdit.Records.All(r => (float)r.GetValue("weight") == 9f));

            Undo.RevertAllDownToGroup(groupBeforeBulkEdit);

            var afterUndo = _repository.Scan(typeof(TestUiItemData));
            Assert.IsTrue(afterUndo.Records.All(r => (float)r.GetValue("weight") == 0f));
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Close the Unity editor if open, then run the batch-mode EditMode test command.
Expected: compile failure — `EditGateway`/`BulkEditor` don't exist yet.

- [ ] **Step 4: Implement `EditGateway` and `BulkEditor`**

```csharp
// Package/Editor/Scry.UI/EditGateway.cs
using System;
using Scry.Core;
using Scry.Core.Unity;
using UnityEditor;

namespace Scry.UI
{
    // The single place every DataGridWindow write goes through. Registering Undo here, right
    // before calling into the repository, means every edit path (single cell, nested cell, bulk)
    // gets undo support for free just by routing through this class instead of the repository
    // directly.
    public static class EditGateway
    {
        public static DataRecord ApplyEdit(ScriptableObjectRepository repository, DataRecord record, string fieldName, object value, Type scriptableObjectType, string undoName)
        {
            RegisterUndo(record, scriptableObjectType, undoName);
            return repository.ApplyEdit(record, fieldName, value, scriptableObjectType);
        }

        public static DataRecord ApplyNestedEdit(ScriptableObjectRepository repository, DataRecord record, string collectionField, int index, string childFieldName, object value, Type scriptableObjectType, string undoName)
        {
            RegisterUndo(record, scriptableObjectType, undoName);
            return repository.ApplyEdit(record, collectionField, index, childFieldName, value, scriptableObjectType);
        }

        private static void RegisterUndo(DataRecord record, Type scriptableObjectType, string undoName)
        {
            var path = AssetDatabase.GUIDToAssetPath(record.Id);
            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as UnityEngine.Object;
            if (asset != null)
                Undo.RegisterCompleteObjectUndo(asset, undoName);
        }
    }
}
```

```csharp
// Package/Editor/Scry.UI/BulkEditor.cs
using System;
using System.Collections.Generic;
using Scry.Core;
using Scry.Core.Unity;
using UnityEditor;

namespace Scry.UI
{
    public static class BulkEditor
    {
        public static void ApplyToSelected(ScriptableObjectRepository repository, IEnumerable<DataRecord> selectedRecords, string fieldName, object value, Type scriptableObjectType, Action<DataRecord> onRecordUpdated)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Bulk edit '{fieldName}'");
            var undoGroup = Undo.GetCurrentGroup();

            foreach (var record in selectedRecords)
            {
                var updated = EditGateway.ApplyEdit(repository, record, fieldName, value, scriptableObjectType, $"Bulk edit '{fieldName}'");
                onRecordUpdated(updated);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Re-run the batch-mode test command. Expected: 2 `EditGatewayTests` + 2 `BulkEditorTests` pass. Delete scratch logs.

- [ ] **Step 6: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/EditGateway.cs Package/Editor/Scry.UI/EditGateway.cs.meta Package/Editor/Scry.UI/BulkEditor.cs Package/Editor/Scry.UI/BulkEditor.cs.meta Package/Tests/Scry.UI.Tests/Fixtures/TestUiItemData.cs Package/Tests/Scry.UI.Tests/Fixtures/TestUiItemData.cs.meta Package/Tests/Scry.UI.Tests/EditGatewayTests.cs Package/Tests/Scry.UI.Tests/EditGatewayTests.cs.meta Package/Tests/Scry.UI.Tests/BulkEditorTests.cs Package/Tests/Scry.UI.Tests/BulkEditorTests.cs.meta
git commit -m "feat(ui): add EditGateway and BulkEditor for undo-wrapped writes"
```

---

### Task 7: `DataGridWindow` shell and the manual-test fixture

**Files:**
- Create: `Package/Editor/Scry.UI/DataGridWindow.cs`
- Create: `TestProject/Assets/ScryManualTest/ManualDropEntry.cs`
- Create: `TestProject/Assets/ScryManualTest/ManualTestMonster.cs`

No automated test — this task's deliverable is the window shell (menu item, `ScryConfig` discovery, tab strip) and the checked-in manual-test fixture that every remaining task in this plan reuses for its manual smoke test, per the architecture doc's stance that `EditorWindow` GUI code stays manual-smoke-tested.

**Interfaces:**
- Consumes: `Scry.Core.Unity.ScryConfig`/`TrackedCollection` (Part A), `Scry.Core.Unity.ScriptableObjectRepository` (Part A).
- Produces: `class DataGridWindow : EditorWindow` with a `[MenuItem("Scry/Data Editor")]` entry point. `_activeState` (a `GridState`, Task 5) and the stub `BuildGrid` method Task 8 replaces. Every later task in this plan extends this file.

- [ ] **Step 1: Create the manual-test fixture**

```csharp
// TestProject/Assets/ScryManualTest/ManualDropEntry.cs
using System;

namespace ScryManualTest
{
    [Serializable]
    public class ManualDropEntry
    {
        public string itemName;
        public float weight;
    }
}
```

```csharp
// TestProject/Assets/ScryManualTest/ManualTestMonster.cs
using System.Collections.Generic;
using Scry.Core.Unity;
using UnityEngine;

namespace ScryManualTest
{
    [ScryCollection]
    public class ManualTestMonster : ScriptableObject
    {
        public string monsterName;
        public int level;
        public bool isBoss;
        public List<ManualDropEntry> dropTable = new List<ManualDropEntry>();
    }
}
```

- [ ] **Step 2: Implement the `DataGridWindow` shell**

```csharp
// Package/Editor/Scry.UI/DataGridWindow.cs
using System;
using System.Collections.Generic;
using Scry.Core.Unity;
using Scry.Core.Unity.Config;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scry.UI
{
    public sealed class DataGridWindow : EditorWindow
    {
        [MenuItem("Scry/Data Editor")]
        public static void Open()
        {
            var window = GetWindow<DataGridWindow>();
            window.titleContent = new GUIContent("Scry Data Editor");
        }

        private readonly ScriptableObjectRepository _repository = new ScriptableObjectRepository();
        private readonly Dictionary<string, int> _idsByRecordId = new Dictionary<string, int>();
        private int _nextId = 1;

        private ScryConfig _config;
        private VisualElement _tabStrip;
        private VisualElement _content;
        private GridState _activeState;

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            _config = FindScryConfig();
            if (_config == null)
            {
                rootVisualElement.Add(new Label("No ScryConfig asset found in the project. Create one via Assets > Create > Scry > Config."));
                return;
            }

            if (_config.TrackedCollections.Count == 0)
            {
                rootVisualElement.Add(new Label("ScryConfig has no tracked collections. Add one via its Inspector."));
                return;
            }

            _tabStrip = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _content = new VisualElement { style = { flexGrow = 1 } };
            rootVisualElement.Add(_tabStrip);
            rootVisualElement.Add(_content);

            foreach (var tracked in _config.TrackedCollections)
            {
                var capturedTracked = tracked;
                var button = new Button(() => SelectTab(capturedTracked)) { text = ShortTypeName(capturedTracked.TypeName) };
                _tabStrip.Add(button);
            }

            SelectTab(_config.TrackedCollections[0]);
        }

        private static ScryConfig FindScryConfig()
        {
            var guids = AssetDatabase.FindAssets("t:ScryConfig");
            if (guids.Length == 0)
                return null;

            if (guids.Length > 1)
                Debug.LogWarning($"Scry: multiple ScryConfig assets found; using the first one ({AssetDatabase.GUIDToAssetPath(guids[0])}).");

            return AssetDatabase.LoadAssetAtPath<ScryConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static string ShortTypeName(string assemblyQualifiedName)
        {
            var commaIndex = assemblyQualifiedName.IndexOf(',');
            var fullName = commaIndex >= 0 ? assemblyQualifiedName.Substring(0, commaIndex) : assemblyQualifiedName;
            var dotIndex = fullName.LastIndexOf('.');
            return dotIndex >= 0 ? fullName.Substring(dotIndex + 1) : fullName;
        }

        private void SelectTab(TrackedCollection tracked)
        {
            var type = Type.GetType(tracked.TypeName);
            _content.Clear();

            if (type == null)
            {
                _content.Add(new Label($"Could not resolve type '{tracked.TypeName}'."));
                return;
            }

            var collection = _repository.Scan(type);
            _activeState = new GridState(type, collection, tracked.Rules);
            _content.Add(BuildGrid(_activeState));
        }

        private int GetOrCreateId(string recordId)
        {
            if (_idsByRecordId.TryGetValue(recordId, out var id))
                return id;

            id = _nextId++;
            _idsByRecordId[recordId] = id;
            return id;
        }

        private VisualElement BuildGrid(GridState state)
        {
            // Task 8 replaces this with the real MultiColumnTreeView.
            return new Label($"{state.Collection.Records.Count} record(s) of '{state.ScriptableObjectType.Name}' (grid not built yet).");
        }
    }
}
```

- [ ] **Step 3: Verify compilation**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\compile_check.txt"
```

Expected: exit code 0, log ends with `Exiting batchmode successfully now!`. Delete `compile_check.txt` after checking.

- [ ] **Step 4: Manual smoke test**

Open the Unity Editor on `TestProject`.

1. Create a `ScryConfig` asset (`Assets > Create > Scry > Config`) anywhere under `Assets/`.
2. Select it, click "Sync Tracked Types" in its Inspector (from Part A's `ScryConfigEditor`) — confirm a `TrackedCollection` entry appears for `ManualTestMonster`.
3. Create 2-3 `ManualTestMonster` assets (`Assets > Create` menu — `ScriptableObject` subclasses without `[CreateAssetMenu]` need `ScriptableObject.CreateInstance` via a quick Editor script, OR add a temporary `[CreateAssetMenu]` to `ManualTestMonster` for this step then remove it — simplest: use the Project window's right-click "Create" if a menu exists, otherwise create one via `ScriptableObject.CreateInstance<ManualTestMonster>()` in the C# Interactive/a throwaway script and `AssetDatabase.CreateAsset`). Give each a distinct `monsterName`, `level`, and a couple of `dropTable` entries.
4. Open `Scry > Data Editor`. Confirm a tab labeled `ManualTestMonster` appears and is selected by default, and the content area shows a record count matching the assets created.

- [ ] **Step 5: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/DataGridWindow.cs Package/Editor/Scry.UI/DataGridWindow.cs.meta TestProject/Assets/ScryManualTest/ManualDropEntry.cs TestProject/Assets/ScryManualTest/ManualDropEntry.cs.meta TestProject/Assets/ScryManualTest/ManualTestMonster.cs TestProject/Assets/ScryManualTest/ManualTestMonster.cs.meta
git commit -m "feat(ui): add DataGridWindow shell with ScryConfig-driven tab strip"
```

---

### Task 8: Grid rendering with wired single-cell editing

**Files:**
- Modify: `Package/Editor/Scry.UI/DataGridWindow.cs`
- Create: `Package/Editor/Scry.UI/GridRow.cs`

No automated test — `MultiColumnTreeView` wiring is manual-smoke-test only. `CellBinder` (Task 1) and `EditGateway` (Task 6), which this task wires together, are already covered by their own tests.

**Interfaces:**
- Consumes: `CellBinder` (Task 1), `EditGateway.ApplyEdit` (Task 6), `GridState` (Task 5).
- Produces: `sealed class GridRow { DataRecord Record; bool IsTopLevel; }`. Replaces `DataGridWindow.BuildGrid`'s stub with a real `MultiColumnTreeView`. Task 9 extends the same tree to add Collection-field detail rows.

- [ ] **Step 1: Add `GridRow`**

```csharp
// Package/Editor/Scry.UI/GridRow.cs
using Scry.Core;

namespace Scry.UI
{
    // One tree row's view model. IsTopLevel distinguishes an actual ScriptableObject record's
    // row from the detail row a Collection field expands into (Task 9) - a detail row's Record
    // is still its PARENT record (there's exactly one detail child per parent, hosting every
    // Collection field's own embedded sub-grid), not a nested entry's own record.
    public sealed class GridRow
    {
        public DataRecord Record { get; }
        public bool IsTopLevel { get; }

        public GridRow(DataRecord record, bool isTopLevel)
        {
            Record = record;
            IsTopLevel = isTopLevel;
        }
    }
}
```

- [ ] **Step 2: Replace `BuildGrid` with the real tree view**

In `Package/Editor/Scry.UI/DataGridWindow.cs`, replace the `BuildGrid` stub with:

```csharp
        private sealed class TreeViewHolder
        {
            public MultiColumnTreeView TreeView;
        }

        private VisualElement BuildGrid(GridState state)
        {
            var holder = new TreeViewHolder();
            var columns = BuildColumns(state, holder);
            var treeView = new MultiColumnTreeView(columns) { style = { flexGrow = 1 } };
            holder.TreeView = treeView;

            RefreshTreeItems(treeView, state);

            return treeView;
        }

        private Columns BuildColumns(GridState state, TreeViewHolder holder)
        {
            var columns = new Columns();
            var fields = new List<Scry.Core.FieldDescriptor>();
            foreach (var field in state.Collection.Schema.Fields)
            {
                if (field.IsSupported)
                    fields.Add(field);
            }

            for (var columnIndex = 0; columnIndex < fields.Count; columnIndex++)
            {
                var field = fields[columnIndex];

                columns.Add(new Column
                {
                    name = field.Name,
                    title = field.Name,
                    makeCell = () => new VisualElement(),
                    bindCell = (container, rowIndex) =>
                    {
                        container.Clear();
                        var row = holder.TreeView.GetItemDataForIndex<GridRow>(rowIndex);

                        if (!row.IsTopLevel)
                            return;

                        var cell = CellBinder.CreateCell(field);
                        CellBinder.BindCell(cell, field, row.Record, newValue => OnCellEdited(state, holder.TreeView, row.Record, field.Name, newValue));
                        container.Add(cell);
                    }
                });
            }

            return columns;
        }

        private void OnCellEdited(GridState state, MultiColumnTreeView treeView, Scry.Core.DataRecord record, string fieldName, object newValue)
        {
            var updated = EditGateway.ApplyEdit(_repository, record, fieldName, newValue, state.ScriptableObjectType, $"Edit {fieldName}");
            state.ReplaceRecord(updated);
            // Every row's DataRecord (including its Fingerprint) must be refreshed after a write,
            // otherwise a second edit to the same row would be checked against a now-stale
            // fingerprint and spuriously throw WriteConflictException.
            RefreshTreeItems(treeView, state);
        }

        private void RefreshTreeItems(MultiColumnTreeView treeView, GridState state)
        {
            var items = new List<TreeViewItemData<GridRow>>();
            foreach (var record in state.Collection.Records)
                items.Add(new TreeViewItemData<GridRow>(GetOrCreateId(record.Id), new GridRow(record, isTopLevel: true)));

            treeView.SetRootItems(items);
            treeView.Rebuild();
        }
```

Add `using UnityEngine.UIElements;` (already present from Task 7) covers `MultiColumnTreeView`/`Columns`/`Column`/`TreeViewItemData<T>` — no new `using` needed.

- [ ] **Step 3: Verify compilation**

Close the Unity editor if open, then run the compile-check command from Task 7 Step 3. Expected: exit code 0. Delete `compile_check.txt` after checking.

- [ ] **Step 4: Manual smoke test**

Open the Unity Editor on `TestProject`, open `Scry > Data Editor`, select the `ManualTestMonster` tab. Confirm:
- Columns `monsterName`, `level`, `isBoss`, `dropTable` appear, each with the right control type (text, integer, toggle, label showing "N entries").
- Values match the manual-test assets from Task 7.
- Editing `monsterName` in a cell and pressing Enter updates the asset — verify by reselecting the tab (forces a re-`Scan`) and confirming the new value persisted.
- Edit the SAME row's `level` field right after editing `monsterName` (without reselecting the tab) — confirms `RefreshTreeItems` kept the row's fingerprint current, i.e. this second edit succeeds without a `WriteConflictException`.

- [ ] **Step 5: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/DataGridWindow.cs Package/Editor/Scry.UI/GridRow.cs Package/Editor/Scry.UI/GridRow.cs.meta
git commit -m "feat(ui): render grid rows/columns and wire single-cell editing"
```

---

### Task 9: Collection-field detail rows (expand/collapse, edit, add/remove entry)

**Files:**
- Modify: `Package/Editor/Scry.UI/DataGridWindow.cs`

No automated test — nested `MultiColumnListView` wiring is manual-smoke-test only. `EditGateway.ApplyNestedEdit` and `ScriptableObjectRepository.AddCollectionEntry`/`RemoveCollectionEntry` (Part A) are already tested.

**Interfaces:**
- Consumes: `EditGateway.ApplyNestedEdit` (Task 6), `ScriptableObjectRepository.AddCollectionEntry`/`RemoveCollectionEntry` (Part A), `CellBinder` (Task 1).
- Produces: extends `DataGridWindow` so every row with at least one Collection field gets a native tree expand arrow revealing an embedded per-Collection-field `MultiColumnListView`, editable and row-add/remove-capable.

- [ ] **Step 1: Build tree items with a detail child, and give the detail row's first column an embedded sub-grid**

In `Package/Editor/Scry.UI/DataGridWindow.cs`, replace `RefreshTreeItems`'s item-building with:

```csharp
        private void RefreshTreeItems(MultiColumnTreeView treeView, GridState state)
        {
            var items = new List<TreeViewItemData<GridRow>>();
            foreach (var record in state.Collection.Records)
                items.Add(BuildTreeItem(record, state.Collection.Schema));

            treeView.SetRootItems(items);
            treeView.Rebuild();
        }

        private TreeViewItemData<GridRow> BuildTreeItem(Scry.Core.DataRecord record, Scry.Core.Schema schema)
        {
            var hasCollectionField = false;
            foreach (var field in schema.Fields)
            {
                if (field.Type == Scry.Core.FieldType.Collection)
                {
                    hasCollectionField = true;
                    break;
                }
            }

            if (!hasCollectionField)
                return new TreeViewItemData<GridRow>(GetOrCreateId(record.Id), new GridRow(record, isTopLevel: true));

            var detailId = GetOrCreateId($"{record.Id}#detail");
            var detailChild = new TreeViewItemData<GridRow>(detailId, new GridRow(record, isTopLevel: false));
            return new TreeViewItemData<GridRow>(GetOrCreateId(record.Id), new GridRow(record, isTopLevel: true), new[] { detailChild });
        }
```

- [ ] **Step 2: Host the embedded sub-grid in the first column's cell for detail rows**

In `BuildColumns`, change the first column's `bindCell` (the loop iteration where `columnIndex == 0`) to handle detail rows:

```csharp
                columns.Add(new Column
                {
                    name = field.Name,
                    title = field.Name,
                    makeCell = () => new VisualElement(),
                    bindCell = (container, rowIndex) =>
                    {
                        container.Clear();
                        var row = holder.TreeView.GetItemDataForIndex<GridRow>(rowIndex);

                        if (!row.IsTopLevel)
                        {
                            if (columnIndex == 0)
                                container.Add(BuildDetailPane(state, holder.TreeView, row.Record));
                            return;
                        }

                        var cell = CellBinder.CreateCell(field);
                        CellBinder.BindCell(cell, field, row.Record, newValue => OnCellEdited(state, holder.TreeView, row.Record, field.Name, newValue));
                        container.Add(cell);
                    }
                });
```

- [ ] **Step 3: Implement `BuildDetailPane` — one embedded sub-grid per Collection field, with add/remove**

```csharp
        private VisualElement BuildDetailPane(GridState state, MultiColumnTreeView treeView, Scry.Core.DataRecord record)
        {
            var container = new VisualElement();

            foreach (var field in state.Collection.Schema.Fields)
            {
                if (field.Type != Scry.Core.FieldType.Collection)
                    continue;

                container.Add(BuildCollectionFieldPane(state, treeView, record, field));
            }

            return container;
        }

        private VisualElement BuildCollectionFieldPane(GridState state, MultiColumnTreeView treeView, Scry.Core.DataRecord record, Scry.Core.FieldDescriptor field)
        {
            var pane = new VisualElement();
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            header.Add(new Label(field.Name) { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
            var addButton = new Button(() =>
            {
                var updated = _repository.AddCollectionEntry(record, field.Name, state.ScriptableObjectType);
                state.ReplaceRecord(updated);
                RefreshTreeItems(treeView, state);
            })
            { text = "+" };
            header.Add(addButton);
            pane.Add(header);

            var entries = record.GetValue(field.Name) as IReadOnlyList<Scry.Core.DataRecord> ?? new List<Scry.Core.DataRecord>();
            var elementFields = new List<Scry.Core.FieldDescriptor>();
            foreach (var elementField in field.ElementSchema.Fields)
            {
                if (elementField.IsSupported)
                    elementFields.Add(elementField);
            }

            var subColumns = new Columns();
            foreach (var elementField in elementFields)
            {
                subColumns.Add(new Column
                {
                    name = elementField.Name,
                    title = elementField.Name,
                    makeCell = () => CellBinder.CreateCell(elementField),
                    bindCell = (cell, entryIndex) =>
                    {
                        var entryRecord = entries[entryIndex];
                        CellBinder.BindCell(cell, elementField, entryRecord, newValue =>
                        {
                            var updated = EditGateway.ApplyNestedEdit(_repository, record, field.Name, entryIndex, elementField.Name, newValue, state.ScriptableObjectType, $"Edit {elementField.Name}");
                            state.ReplaceRecord(updated);
                            RefreshTreeItems(treeView, state);
                        });
                    }
                });
            }

            subColumns.Add(new Column
            {
                name = "__remove",
                title = string.Empty,
                width = 30,
                makeCell = () => new Button { text = "-" },
                bindCell = (cell, entryIndex) =>
                {
                    ((Button)cell).clicked += () =>
                    {
                        var updated = _repository.RemoveCollectionEntry(record, field.Name, entryIndex, state.ScriptableObjectType);
                        state.ReplaceRecord(updated);
                        RefreshTreeItems(treeView, state);
                    };
                }
            });

            var subGrid = new MultiColumnListView(subColumns)
            {
                itemsSource = (System.Collections.IList)entries,
                fixedItemHeight = 22,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
            };
            pane.Add(subGrid);

            return pane;
        }
```

Note: the remove button's `bindCell` uses `+=` rather than replacing the handler — since `makeCell` allocates a fresh `Button` per virtualized slot and `RefreshTreeItems` fully rebuilds the tree (including detail panes) after every edit in this plan, slot reuse across different records' remove buttons doesn't currently happen within a single pane's lifetime. If a future change makes `MultiColumnListView` rows persist across edits without a full rebuild, this would need the same unregister-before-register treatment `CellBinder` already uses — noted here rather than solved speculatively (YAGNI).

- [ ] **Step 4: Verify compilation**

Close the Unity editor if open, then run the compile-check command. Expected: exit code 0. Delete `compile_check.txt` after checking.

- [ ] **Step 5: Manual smoke test**

Open the Unity Editor on `TestProject`, open `Scry > Data Editor`, select `ManualTestMonster`. Confirm:
- Each row shows a native tree expand arrow (since every `ManualTestMonster` has a `dropTable` field).
- Expanding a row reveals a `dropTable` sub-grid with `itemName`/`weight` columns and a remove ("-") column, showing that record's actual entries.
- Clicking "+" adds a new blank entry; editing its `itemName`/`weight` persists (verify via reselecting the tab).
- Clicking "-" on an entry removes it and the sub-grid updates.
- Collapsing and re-expanding the row still shows the correct current entries.

- [ ] **Step 6: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/DataGridWindow.cs
git commit -m "feat(ui): add Collection-field detail rows with add/remove/edit"
```

---

### Task 10: Bulk-edit and search/filter toolbars

**Files:**
- Modify: `Package/Editor/Scry.UI/DataGridWindow.cs`

No automated test — toolbar wiring is manual-smoke-test only. `BulkEditor` (Task 6) and `RowFilter` (Task 2) are already tested.

**Interfaces:**
- Consumes: `BulkEditor.ApplyToSelected` (Task 6), `RowFilter.Matches`/`ColumnFilter` (Task 2).
- Produces: a toolbar above the grid with a multi-select-aware bulk-edit control and a search box; row visibility driven by `RowFilter`.

- [ ] **Step 1: Add multi-select tracking and a bulk-edit toolbar**

In `DataGridWindow`, add a field to track selection and a toolbar builder, and call it from `SelectTab`:

```csharp
        private readonly HashSet<string> _selectedRecordIds = new HashSet<string>();

        private VisualElement BuildToolbar(GridState state, MultiColumnTreeView treeView)
        {
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            var fieldNameDropdown = new PopupField<string>(GetEditableFieldNames(state.Collection.Schema), 0);
            var valueField = new TextField { style = { minWidth = 80 } };
            var applyButton = new Button(() =>
            {
                var selected = GetSelectedRecords(state);
                if (selected.Count == 0)
                    return;

                var fieldDescriptor = state.Collection.Schema.GetField(fieldNameDropdown.value);
                var parsedValue = ParseValueForField(fieldDescriptor, valueField.value);

                BulkEditor.ApplyToSelected(_repository, selected, fieldNameDropdown.value, parsedValue, state.ScriptableObjectType, updated => state.ReplaceRecord(updated));
                RefreshTreeItems(treeView, state);
            })
            { text = "Apply to selected" };

            toolbar.Add(fieldNameDropdown);
            toolbar.Add(valueField);
            toolbar.Add(applyButton);

            return toolbar;
        }

        private static List<string> GetEditableFieldNames(Scry.Core.Schema schema)
        {
            var names = new List<string>();
            foreach (var field in schema.Fields)
            {
                if (field.IsSupported && field.Type != Scry.Core.FieldType.Collection)
                    names.Add(field.Name);
            }
            return names;
        }

        private static object ParseValueForField(Scry.Core.FieldDescriptor field, string rawText)
        {
            switch (field.Type)
            {
                case Scry.Core.FieldType.Numeric:
                    return float.TryParse(rawText, out var f) ? f : 0f;
                case Scry.Core.FieldType.Boolean:
                    return bool.TryParse(rawText, out var b) && b;
                case Scry.Core.FieldType.Enum:
                    return int.TryParse(rawText, out var i) ? i : 0;
                default:
                    return rawText;
            }
        }

        private List<Scry.Core.DataRecord> GetSelectedRecords(GridState state)
        {
            var selected = new List<Scry.Core.DataRecord>();
            foreach (var record in state.Collection.Records)
            {
                if (_selectedRecordIds.Contains(record.Id))
                    selected.Add(record);
            }
            return selected;
        }
```

Add a checkbox column as the new first column in `BuildColumns` (before the field-derived columns), toggling membership in `_selectedRecordIds`:

```csharp
            var selectionColumn = new Column
            {
                name = "__selected",
                title = string.Empty,
                width = 24,
                makeCell = () => new Toggle(),
                bindCell = (cell, rowIndex) =>
                {
                    var row = holder.TreeView.GetItemDataForIndex<GridRow>(rowIndex);
                    var toggle = (Toggle)cell;
                    if (!row.IsTopLevel)
                    {
                        toggle.style.display = DisplayStyle.None;
                        return;
                    }
                    toggle.style.display = DisplayStyle.Flex;
                    toggle.SetValueWithoutNotify(_selectedRecordIds.Contains(row.Record.Id));
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.newValue)
                            _selectedRecordIds.Add(row.Record.Id);
                        else
                            _selectedRecordIds.Remove(row.Record.Id);
                    });
                }
            };
            columns.Add(selectionColumn);
```

(This selection toggle has the same rebind-callback-accumulation risk `CellBinder` solves for its own controls — acceptable for v1 since `RefreshTreeItems` fully rebuilds the tree, and thus reallocates cells via `makeCell`, after every edit that would otherwise cause a stale rebind; flagged here rather than fully re-solved, consistent with the `BuildCollectionFieldPane` note in Task 9.)

- [ ] **Step 2: Add the search box and wire filtering**

`MultiColumnTreeView` has no per-row show/hide API, so filtering works by rebuilding `SetRootItems` with only the matching records — `RefreshTreeItems` (from Task 8) is already the single place that builds the item list, so filtering belongs there:

```csharp
        private string _searchText = string.Empty;

        private VisualElement BuildSearchBox(GridState state, MultiColumnTreeView treeView)
        {
            var searchField = new ToolbarSearchField();
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue;
                RefreshTreeItems(treeView, state);
            });
            return searchField;
        }
```

Change `RefreshTreeItems` (defined in Task 8, extended with detail children in Task 9) to filter by `_searchText` before building items:

```csharp
        private void RefreshTreeItems(MultiColumnTreeView treeView, GridState state)
        {
            var items = new List<TreeViewItemData<GridRow>>();
            foreach (var record in state.Collection.Records)
            {
                if (!RowFilter.Matches(record, state.Collection.Schema, _searchText, null))
                    continue;

                items.Add(BuildTreeItem(record, state.Collection.Schema));
            }

            treeView.SetRootItems(items);
            treeView.Rebuild();
        }
```

**Explicitly out of scope for this task:** per-column filter controls (`ColumnFilter`, already implemented and tested in Task 2) are not wired into the column headers here — only the global search box is. `RowFilter.Matches`'s `columnFilters` parameter is ready for a future task to populate from per-column header UI and pass through to the same call site above; wiring that UI (dropdowns/range fields per column type) is deferred, the same way Part A deferred a custom rule-authoring UI beyond the default Inspector.

- [ ] **Step 3: Wire the toolbars into `SelectTab`**

```csharp
        private void SelectTab(TrackedCollection tracked)
        {
            var type = Type.GetType(tracked.TypeName);
            _content.Clear();
            _selectedRecordIds.Clear();
            _searchText = string.Empty;

            if (type == null)
            {
                _content.Add(new Label($"Could not resolve type '{tracked.TypeName}'."));
                return;
            }

            var collection = _repository.Scan(type);
            _activeState = new GridState(type, collection, tracked.Rules);

            var grid = BuildGrid(_activeState);
            var treeView = (MultiColumnTreeView)grid;

            _content.Add(BuildSearchBox(_activeState, treeView));
            _content.Add(BuildToolbar(_activeState, treeView));
            _content.Add(grid);
        }
```

Add `using UnityEditor.UIElements;` to the top of `DataGridWindow.cs` for `ToolbarSearchField`/`PopupField` (both are in `UnityEditor.UIElements`/`UnityEngine.UIElements` respectively — `PopupField<T>` is in `UnityEngine.UIElements`, already covered; `ToolbarSearchField` is in `UnityEditor.UIElements`, needs the new `using`).

- [ ] **Step 4: Verify compilation**

Close the Unity editor if open, then run the compile-check command. Expected: exit code 0. Delete `compile_check.txt` after checking.

- [ ] **Step 5: Manual smoke test**

Open the Unity Editor on `TestProject`, open `Scry > Data Editor`, select `ManualTestMonster`.

- Type part of one monster's name into the search box — confirm only matching rows remain, and clearing the box restores all rows.
- Check the selection toggle on 2 rows, pick `level` in the bulk-edit dropdown, type a number, click "Apply to selected" — confirm both rows' `level` updated (verify via reselecting the tab).

- [ ] **Step 6: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/DataGridWindow.cs
git commit -m "feat(ui): add bulk-edit and search toolbars"
```

---

### Task 11: Validation surfacing and write-conflict handling

**Files:**
- Modify: `Package/Editor/Scry.UI/DataGridWindow.cs`

No automated test — cell decoration and dialogs are manual-smoke-test only. `GridState`/`ValidationRunner` (Task 5), `ValidationIssueIndex`/`ValidationIssueLocator` (Tasks 3-4), and the underlying `WriteConflictException` (Part A) are already tested.

**Interfaces:**
- Consumes: `GridState.Issues` (Task 5), `ValidationIssueIndex.HighestSeverityFor` (Task 4), `ValidationIssueLocator.Locate` (Task 3), `Scry.Core.Unity.WriteConflictException` (Part A).
- Produces: colored issue icons on affected cells, a collapsible issues panel, and a reload-prompt dialog on write conflicts. This is the last task in this plan.

- [ ] **Step 1: Decorate cells with validation icons**

In `BuildColumns`'s field-column `bindCell` (the branch handling `row.IsTopLevel`), append an icon after binding the cell:

```csharp
                        var cell = CellBinder.CreateCell(field);
                        CellBinder.BindCell(cell, field, row.Record, newValue => OnCellEdited(state, holder.TreeView, row.Record, field.Name, newValue));
                        container.Add(cell);

                        var severity = state.Issues.HighestSeverityFor(row.Record.Id, field.Name);
                        if (severity.HasValue)
                        {
                            var issues = state.Issues.IssuesFor(row.Record.Id, field.Name);
                            var icon = new Label(severity.Value == Scry.Core.ValidationSeverity.Error ? "!" : "?")
                            {
                                tooltip = string.Join("\n", issues.Select(i => i.Message)),
                                style =
                                {
                                    color = severity.Value == Scry.Core.ValidationSeverity.Error ? Color.red : Color.yellow,
                                    unityFontStyleAndWeight = FontStyle.Bold
                                }
                            };
                            container.Add(icon);
                        }
```

Add `using System.Linq;` to the top of `DataGridWindow.cs` — Tasks 7-10's code doesn't use LINQ, so this is a new `using` needed for the `.Select`/`.FirstOrDefault` calls in this task's code.

- [ ] **Step 2: Add the issues panel**

```csharp
        private VisualElement BuildIssuesPanel(GridState state, MultiColumnTreeView treeView)
        {
            var foldout = new Foldout { text = $"Issues ({state.Issues.AllIssues.Count})", value = false };

            foreach (var issue in state.Issues.AllIssues)
            {
                var location = ValidationIssueLocator.Locate(issue);
                var row = new Button(() => JumpToIssue(state, treeView, location))
                {
                    text = $"[{issue.Severity}] {issue.FieldName}: {issue.Message}"
                };
                foldout.Add(row);
            }

            return foldout;
        }

        private void JumpToIssue(GridState state, MultiColumnTreeView treeView, IssueLocation location)
        {
            if (location.IsCollectionWide || location.ParentRecordId == null)
                return;

            var id = GetOrCreateId(location.ParentRecordId);
            treeView.SetSelectionById(id);
            treeView.ScrollToItemById(id);

            if (location.ChildIndex.HasValue || location.NestedFieldGroupKey != null)
            {
                var detailId = GetOrCreateId($"{location.ParentRecordId}#detail");
                treeView.ExpandItem(detailId);
            }
        }
```

Rebuild the issues panel whenever the grid refreshes — in `SelectTab`, after adding the grid:

```csharp
            _content.Add(BuildSearchBox(_activeState, treeView));
            _content.Add(BuildToolbar(_activeState, treeView));
            _content.Add(grid);
            _content.Add(BuildIssuesPanel(_activeState, treeView));
```

The issues panel shows a stale count/list after an edit changes validation results within the same tab session (since it isn't rebuilt by `RefreshTreeItems`) — rebuilding it live on every edit is a reasonable fast-follow, not required for this task's manual smoke test, which only exercises the initial render and click-to-navigate.

- [ ] **Step 3: Handle write conflicts as a reload-prompt dialog**

Every write call site added in Tasks 8-10 needs the same `try`/`catch (WriteConflictException)` treatment. Add the shared dialog helper first, then update each call site to use it.

```csharp
        private void PromptReloadOnConflict(GridState state, MultiColumnTreeView treeView, string recordId)
        {
            if (!EditorUtility.DisplayDialog("Scry", $"The asset for '{recordId}' changed outside this window since it was loaded. Reload the collection to see the latest data?", "Reload", "Cancel"))
                return;

            var refreshed = _repository.Scan(state.ScriptableObjectType);
            var refreshedRecord = refreshed.Records.FirstOrDefault(r => r.Id == recordId);
            if (refreshedRecord != null)
                state.ReplaceRecord(refreshedRecord);
            RefreshTreeItems(treeView, state);
        }
```

Update `OnCellEdited` (Task 8) to use it:

```csharp
        private void OnCellEdited(GridState state, MultiColumnTreeView treeView, Scry.Core.DataRecord record, string fieldName, object newValue)
        {
            try
            {
                var updated = EditGateway.ApplyEdit(_repository, record, fieldName, newValue, state.ScriptableObjectType, $"Edit {fieldName}");
                state.ReplaceRecord(updated);
                RefreshTreeItems(treeView, state);
            }
            catch (Scry.Core.Unity.WriteConflictException)
            {
                PromptReloadOnConflict(state, treeView, record.Id);
            }
        }
```

In `BuildCollectionFieldPane` (Task 9), replace the nested-edit callback with:

```csharp
                        CellBinder.BindCell(cell, elementField, entryRecord, newValue =>
                        {
                            try
                            {
                                var updated = EditGateway.ApplyNestedEdit(_repository, record, field.Name, entryIndex, elementField.Name, newValue, state.ScriptableObjectType, $"Edit {elementField.Name}");
                                state.ReplaceRecord(updated);
                                RefreshTreeItems(treeView, state);
                            }
                            catch (Scry.Core.Unity.WriteConflictException)
                            {
                                PromptReloadOnConflict(state, treeView, record.Id);
                            }
                        });
```

and the "+"/"-" button handlers with:

```csharp
            var addButton = new Button(() =>
            {
                try
                {
                    var updated = _repository.AddCollectionEntry(record, field.Name, state.ScriptableObjectType);
                    state.ReplaceRecord(updated);
                    RefreshTreeItems(treeView, state);
                }
                catch (Scry.Core.Unity.WriteConflictException)
                {
                    PromptReloadOnConflict(state, treeView, record.Id);
                }
            })
            { text = "+" };
```

```csharp
                    ((Button)cell).clicked += () =>
                    {
                        try
                        {
                            var updated = _repository.RemoveCollectionEntry(record, field.Name, entryIndex, state.ScriptableObjectType);
                            state.ReplaceRecord(updated);
                            RefreshTreeItems(treeView, state);
                        }
                        catch (Scry.Core.Unity.WriteConflictException)
                        {
                            PromptReloadOnConflict(state, treeView, record.Id);
                        }
                    };
```

In `BuildToolbar` (Task 10), replace the apply-button handler with:

```csharp
            var applyButton = new Button(() =>
            {
                var selected = GetSelectedRecords(state);
                if (selected.Count == 0)
                    return;

                var fieldDescriptor = state.Collection.Schema.GetField(fieldNameDropdown.value);
                var parsedValue = ParseValueForField(fieldDescriptor, valueField.value);

                try
                {
                    BulkEditor.ApplyToSelected(_repository, selected, fieldNameDropdown.value, parsedValue, state.ScriptableObjectType, updated => state.ReplaceRecord(updated));
                    RefreshTreeItems(treeView, state);
                }
                catch (Scry.Core.Unity.WriteConflictException conflict)
                {
                    PromptReloadOnConflict(state, treeView, conflict.RecordId);
                }
            })
            { text = "Apply to selected" };
```

All four call sites now share `PromptReloadOnConflict`, defined once at the top of this step.

- [ ] **Step 4: Verify compilation**

Close the Unity editor if open, then run the compile-check command. Expected: exit code 0. Delete `compile_check.txt` after checking.

- [ ] **Step 5: Manual smoke test**

Open the Unity Editor on `TestProject`. First, give one of the manual-test `ScryConfig`'s rules something to catch: on the `ScryConfig` asset from Task 7, add a `SumEqualsRuleConfig` to the `ManualTestMonster` entry's `Rules` list via the default Inspector (`Field = "level"`, `Target` = some value none of the manual-test assets' `level`s sum to).

1. Open `Scry > Data Editor`, select `ManualTestMonster`. Confirm a "!" or "?" icon appears on the `level` cell of the row(s) the rule's `ValidationIssue.RecordId` points at (per the SumEqualsRule's non-nested/grouped semantics from Part A), with the issue message in its tooltip.
2. Expand the "Issues" foldout at the bottom — confirm it lists the issue with severity, field, and message; click it and confirm the grid scrolls to/selects the matching row.
3. Test the conflict path: with the window open, select an asset in the Project window and edit the same field directly in the default Inspector (outside the grid) — then edit that same row's cell in the Scry grid. Confirm a "Reload" dialog appears rather than a silent overwrite; clicking "Reload" re-scans and shows the externally-changed value.

- [ ] **Step 6: Confirm `.meta` files and commit**

```bash
git status --short
git add Package/Editor/Scry.UI/DataGridWindow.cs
git commit -m "feat(ui): surface validation issues and handle write conflicts"
```

---

## Summary

After Task 11, `Scry.UI`'s `DataGridWindow` provides a working Pillar 1 data editor: tab-per-tracked-collection, a spreadsheet grid with per-`FieldType` cell controls, expandable Collection-field sub-tables with add/remove, undo-wrapped single/nested/bulk editing, global search, inline validation decoration with a clickable issues panel, and write-conflict reload prompts — closing the loop the final Part A review flagged (`ScryConfig`/`RuleConfig`/`RuleFactory` now have a real, non-test consumer via `ValidationRunner`). Formulas, CSV/JSON import/export, and a richer rule-authoring UI remain explicitly out of scope, per the design spec.

**Implementation decisions made while writing this plan, beyond the spec's higher-level description:**
- The spec described Collection-field expansion as `MultiColumnTreeView`'s native hierarchy, "columned by the element schema's fields." `MultiColumnTreeView` shares one `Columns` definition across every depth, which doesn't fit a child row whose fields differ entirely from its parent's. Task 9 instead uses the tree's native expand/collapse (so the approved widget and its UX — arrow, indentation, keyboard nav — are unchanged) but hosts an **embedded `MultiColumnListView`** in the detail row's first cell, columned by `ElementSchema` inside that embedded grid rather than the outer tree's own headers. This satisfies the spec's UX description ("clicking expands an inline nested grid directly under that row") while being implementable against the real API.
- Per-column filter UI (dropdowns/range fields in each column header) is explicitly deferred — `RowFilter`/`ColumnFilter` (Task 2) already implement and test the matching logic, ready for that UI whenever it's built; only the global search box is wired into `DataGridWindow` in this plan.
