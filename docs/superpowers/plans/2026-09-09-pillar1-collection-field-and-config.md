# Pillar 1 Part A: Collection Field & Config System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `Core` and `Core.Unity` so a `List<T>`/`T[]` field of a plain `[Serializable]` class (e.g. a creature's list of weighted drop entries) can be scanned, edited, and validated as a `Collection` field, and add the `ScryConfig` asset that lets a user declare which `ScriptableObject` types to track and which validation rules apply to each — entirely as data, no code required. This is the data-layer half of Pillar 1; the UI grid that renders it is a separate follow-up plan.

**Architecture:** Extends the existing two-assembly split with no new assemblies. `Core` gains a `Collection` `FieldType`, a nested-`Schema`-aware `FieldDescriptor`, and `nestedField`-aware validation rules. `Core.Unity` gains list-detection in `SchemaMapper`, array read/write/insert/delete in `ScriptableObjectRepository`, and a new config surface (`ScryConfig`, `TrackedCollection`, `RuleConfig` subclasses, `RuleFactory`, `[ScryCollection]`, `ScryConfigSync`).

**Tech Stack:** C# / .NET (netstandard2.1 for `Core`, Editor-only for `Core.Unity`), NUnit for both plain `dotnet test` (`Core`) and Unity Test Framework EditMode tests (`Core.Unity`), Unity 6000.3.10f1.

**Spec:** [`docs/superpowers/specs/2026-09-09-pillar1-data-editor-design.md`](../specs/2026-09-09-pillar1-data-editor-design.md) — Sections 1-3 (Data model extensions; Config & rule authoring; `Core.Unity` changes for Collection fields). Section 4 (`UI` layer) is out of scope for this plan. Also extends [`docs/superpowers/specs/2026-07-16-architecture-design.md`](../specs/2026-07-16-architecture-design.md).

## Global Constraints

- `Core` stays plain .NET with **zero** `UnityEngine`/`UnityEditor` reference — enforced by `Scry.Core.asmdef`'s `noEngineReferences: true`. `RuleConfig` and `ScryConfig` types are Unity-serializable and therefore live in `Core.Unity`, not `Core`.
- Only single-level nesting is modeled: `List<T>`/`T[]` where `T` is a plain `[Serializable]` class (not a `UnityEngine.Object`). Anything deeper (`List<List<T>>`) or unrecognized (`List<int>`, `Dictionary<,>`) stays `FieldType.Unsupported` — never throws, never aborts the scan (spec Section 1, and the existing graceful-degradation principle already in `SchemaMapper`).
- Rules stay data, never code: a rule is authored as a `RuleConfig` subclass on a `ScryConfig` asset, edited via Unity's default Inspector (`[SerializeReference]` makes the polymorphic `List<RuleConfig>` render correctly with no custom `PropertyDrawer`).
- `[ScryCollection]` is discovery/convenience only — it never carries rules, and a type without it can still be tracked manually.
- Write-back conflict detection (`WriteConflictException`) is unchanged and applies identically to nested-field edits, since `AssetDatabase.GetAssetDependencyHash` already covers full asset content including array data.
- Every `Core`/`Core.Unity` operation must remain callable headlessly, per the existing architecture doc.

---

## File Structure

```
Package/
  Runtime/Scry.Core/
    FieldType.cs                          # modify: add Collection
    FieldDescriptor.cs                    # modify: add ElementSchema
    Rules/
      SumEqualsRule.cs                    # modify: add nestedField
      NoDuplicateRule.cs                  # modify: add nestedField
      RequiredAtLeastOnceRule.cs          # modify: add nestedField
  Editor/Scry.Core.Unity/
    SchemaMapper.cs                       # modify: detect List<T>/T[] of [Serializable] classes
    ScriptableObjectRepository.cs         # modify: Scan/ApplyEdit for Collection fields, AddCollectionEntry/RemoveCollectionEntry
    ScryCollectionAttribute.cs            # new
    ScryConfig.cs                         # new
    ScryConfigEditor.cs                   # new
    ScryConfigSync.cs                     # new
    RuleFactory.cs                        # new
    Config/
      RuleConfig.cs                       # new (abstract base)
      TrackedCollection.cs                # new
      SumEqualsRuleConfig.cs              # new
      NoDuplicateRuleConfig.cs            # new
      RequiredAtLeastOnceRuleConfig.cs    # new
  Tests/
    Scry.Core.Tests/
      FieldDescriptorTests.cs             # modify: append ElementSchema tests
      Rules/
        SumEqualsRuleTests.cs             # modify: append nestedField tests
        NoDuplicateRuleTests.cs           # modify: append nestedField tests
        RequiredAtLeastOnceRuleTests.cs   # modify: append nestedField tests
    Scry.Core.Unity.Tests/
      Fixtures/
        TestDropEntry.cs                  # new
        TestMonsterData.cs                # new
        TestOuterEntry.cs                 # new
        TestNestedListData.cs             # new
        TestScryCollectionAttributedData.cs  # new
      SchemaMapperTests.cs                # modify: append Collection detection tests
      ScriptableObjectRepositoryTests.cs  # modify: append Collection scan/edit/add/remove tests
      RuleFactoryTests.cs                 # new
      ScryConfigTests.cs                  # new
      ScryConfigSyncTests.cs              # new
```

---

### Task 1: `Core` — `FieldType.Collection` and `FieldDescriptor.ElementSchema`

**Files:**
- Modify: `Package/Runtime/Scry.Core/FieldType.cs`
- Modify: `Package/Runtime/Scry.Core/FieldDescriptor.cs`
- Test: `Package/Tests/Scry.Core.Tests/FieldDescriptorTests.cs`

**Interfaces:**
- Produces: `FieldType.Collection`; `FieldDescriptor(string name, FieldType type, Schema elementSchema = null)`, `FieldDescriptor.ElementSchema` (`Schema`, null unless `Type == Collection`). Consumed by every later task in this plan.

- [ ] **Step 1: Write the failing tests**

Append to `Package/Tests/Scry.Core.Tests/FieldDescriptorTests.cs` (inside the existing `FieldDescriptorTests` class, after `Constructor_ThrowsOnEmptyName`):

```csharp
        [Test]
        public void Constructor_SetsElementSchema_ForCollectionType()
        {
            var elementSchema = new Schema("DropEntry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var field = new FieldDescriptor("dropTable", FieldType.Collection, elementSchema);

            Assert.That(field.Type, Is.EqualTo(FieldType.Collection));
            Assert.That(field.ElementSchema, Is.SameAs(elementSchema));
        }

        [Test]
        public void Constructor_ElementSchemaDefaultsToNull()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);

            Assert.That(field.ElementSchema, Is.Null);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: compile failure — `FieldDescriptor` has no 3-argument constructor and no `ElementSchema` member yet.

- [ ] **Step 3: Add `Collection` to `FieldType`**

```csharp
// Package/Runtime/Scry.Core/FieldType.cs
namespace Scry.Core
{
    public enum FieldType
    {
        Numeric,
        String,
        Boolean,
        Enum,
        Reference,
        Collection,
        Unsupported
    }
}
```

- [ ] **Step 4: Add `ElementSchema` to `FieldDescriptor`**

```csharp
// Package/Runtime/Scry.Core/FieldDescriptor.cs
using System;

namespace Scry.Core
{
    public sealed class FieldDescriptor
    {
        public string Name { get; }
        public FieldType Type { get; }
        public Schema ElementSchema { get; }
        public bool IsSupported => Type != FieldType.Unsupported;

        public FieldDescriptor(string name, FieldType type, Schema elementSchema = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Field name must not be empty.", nameof(name));

            Name = name;
            Type = type;
            ElementSchema = elementSchema;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: all tests pass, including the 2 new ones.

- [ ] **Step 6: Commit**

```bash
git add Package/Runtime/Scry.Core/FieldType.cs Package/Runtime/Scry.Core/FieldDescriptor.cs Package/Tests/Scry.Core.Tests/FieldDescriptorTests.cs
git commit -m "feat(core): add Collection field type and FieldDescriptor.ElementSchema"
```

---

### Task 2: `Core` — `SumEqualsRule` nested-field support

**Files:**
- Modify: `Package/Runtime/Scry.Core/Rules/SumEqualsRule.cs`
- Test: `Package/Tests/Scry.Core.Tests/Rules/SumEqualsRuleTests.cs`

**Interfaces:**
- Consumes: `DataCollection`, `DataRecord.GetValue` (existing), `Schema`/`FieldDescriptor.Collection` (Task 1, for the test's collection field).
- Produces: `SumEqualsRule(string field, double target, double tolerance = 0.0001, string groupByField = null, string nestedField = null)`. When `nestedField` is set, `Evaluate` sums `field` within each parent record's nested list (`parent.GetValue(nestedField) as IReadOnlyList<DataRecord>`) instead of across the top-level collection, yielding one issue per offending parent (`recordId` = the parent's `Id`, or `"{parentId}/{groupKey}"` if `groupByField` is also set).

- [ ] **Step 1: Write the failing test**

Append to `Package/Tests/Scry.Core.Tests/Rules/SumEqualsRuleTests.cs` (inside the existing class, after `Evaluate_RespectsTolerance`):

```csharp
        private static DataCollection BuildParentCollection(params (string parentId, List<DataRecord> dropTable)[] parents)
        {
            var elementSchema = new Schema("DropEntry", new[]
            {
                new FieldDescriptor("itemId", FieldType.String),
                new FieldDescriptor("weight", FieldType.Numeric)
            });
            var schema = new Schema("Monster", new[]
            {
                new FieldDescriptor("dropTable", FieldType.Collection, elementSchema)
            });
            var records = parents.Select(p => new DataRecord(p.parentId, new Dictionary<string, object>
            {
                ["dropTable"] = (IReadOnlyList<DataRecord>)p.dropTable
            }));
            return new DataCollection(schema, records);
        }

        [Test]
        public void Evaluate_NestedField_ReportsIssuePerParent_WhenNestedSumDoesNotMatchTarget()
        {
            var goblinDrops = new List<DataRecord>
            {
                new DataRecord("goblin#0", new Dictionary<string, object> { ["itemId"] = "sword", ["weight"] = 60.0 }),
                new DataRecord("goblin#1", new Dictionary<string, object> { ["itemId"] = "shield", ["weight"] = 30.0 })
            };
            var wolfDrops = new List<DataRecord>
            {
                new DataRecord("wolf#0", new Dictionary<string, object> { ["itemId"] = "pelt", ["weight"] = 100.0 })
            };

            var collection = BuildParentCollection(("goblin", goblinDrops), ("wolf", wolfDrops));
            var rule = new SumEqualsRule("weight", target: 100, nestedField: "dropTable");

            var issues = rule.Evaluate(collection).ToList();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].RecordId, Is.EqualTo("goblin"));
        }

        [Test]
        public void Evaluate_NestedField_NoIssue_WhenAllParentsSumToTarget()
        {
            var goblinDrops = new List<DataRecord>
            {
                new DataRecord("goblin#0", new Dictionary<string, object> { ["itemId"] = "sword", ["weight"] = 60.0 }),
                new DataRecord("goblin#1", new Dictionary<string, object> { ["itemId"] = "shield", ["weight"] = 40.0 })
            };
            var wolfDrops = new List<DataRecord>
            {
                new DataRecord("wolf#0", new Dictionary<string, object> { ["itemId"] = "pelt", ["weight"] = 100.0 })
            };

            var collection = BuildParentCollection(("goblin", goblinDrops), ("wolf", wolfDrops));
            var rule = new SumEqualsRule("weight", target: 100, nestedField: "dropTable");

            Assert.That(rule.Evaluate(collection), Is.Empty);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: compile failure — `SumEqualsRule` has no `nestedField` parameter yet.

- [ ] **Step 3: Implement nested-field support**

```csharp
// Package/Runtime/Scry.Core/Rules/SumEqualsRule.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core.Rules
{
    public sealed class SumEqualsRule : ValidationRule
    {
        private const string UngroupedKey = "*";

        private readonly string _field;
        private readonly double _target;
        private readonly double _tolerance;
        private readonly string _groupByField;
        private readonly string _nestedField;

        public SumEqualsRule(string field, double target, double tolerance = 0.0001, string groupByField = null, string nestedField = null)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _target = target;
            _tolerance = tolerance;
            _groupByField = groupByField;
            _nestedField = nestedField;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            if (_nestedField == null)
            {
                foreach (var issue in EvaluateGroups(collection.Records, groupKey => groupKey))
                    yield return issue;
                yield break;
            }

            foreach (var parent in collection.Records)
            {
                var nested = parent.GetValue(_nestedField) as IReadOnlyList<DataRecord>;
                if (nested == null)
                    continue;

                foreach (var issue in EvaluateGroups(nested, groupKey => groupKey == UngroupedKey ? parent.Id : $"{parent.Id}/{groupKey}"))
                    yield return issue;
            }
        }

        private IEnumerable<ValidationIssue> EvaluateGroups(IEnumerable<DataRecord> records, Func<string, string> recordIdSelector)
        {
            var groups = _groupByField == null
                ? new[] { (Key: UngroupedKey, Records: records) }
                : records
                    .GroupBy(r => Convert.ToString(r.GetValue(_groupByField)))
                    .Select(g => (Key: g.Key, Records: (IEnumerable<DataRecord>)g))
                    .ToArray();

            foreach (var group in groups)
            {
                var sum = group.Records.Sum(r => Convert.ToDouble(r.GetValue(_field) ?? 0));

                if (Math.Abs(sum - _target) > _tolerance)
                {
                    yield return new ValidationIssue(
                        recordId: recordIdSelector(group.Key),
                        fieldName: _field,
                        message: $"Sum of '{_field}' in group '{group.Key}' is {sum}, expected {_target}.");
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: all tests pass, including the 2 new ones and the 4 pre-existing `SumEqualsRuleTests` (unchanged behavior for the non-nested path).

- [ ] **Step 5: Commit**

```bash
git add Package/Runtime/Scry.Core/Rules/SumEqualsRule.cs Package/Tests/Scry.Core.Tests/Rules/SumEqualsRuleTests.cs
git commit -m "feat(core): add nestedField support to SumEqualsRule"
```

---

### Task 3: `Core` — `NoDuplicateRule` nested-field support

**Files:**
- Modify: `Package/Runtime/Scry.Core/Rules/NoDuplicateRule.cs`
- Test: `Package/Tests/Scry.Core.Tests/Rules/NoDuplicateRuleTests.cs`

**Interfaces:**
- Produces: `NoDuplicateRule(string field, string nestedField = null)`. When `nestedField` is set, duplicate-detection resets per parent record (a value repeated in two different parents' nested lists is not a duplicate), yielding issues with `recordId` = the nested child's own id (`"{parentId}#{index}"`, matching the id scheme `ScriptableObjectRepository` will use in Task 6).

- [ ] **Step 1: Write the failing test**

Append to `Package/Tests/Scry.Core.Tests/Rules/NoDuplicateRuleTests.cs` (inside the existing class, after `Evaluate_IgnoresNullValues`):

```csharp
        private static DataCollection BuildParentCollection(params (string parentId, List<DataRecord> dropTable)[] parents)
        {
            var elementSchema = new Schema("DropEntry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var schema = new Schema("Monster", new[] { new FieldDescriptor("dropTable", FieldType.Collection, elementSchema) });
            var records = parents.Select(p => new DataRecord(p.parentId, new Dictionary<string, object>
            {
                ["dropTable"] = (IReadOnlyList<DataRecord>)p.dropTable
            }));
            return new DataCollection(schema, records);
        }

        [Test]
        public void Evaluate_NestedField_ReportsDuplicate_WithinSameParentOnly()
        {
            var goblinDrops = new List<DataRecord>
            {
                new DataRecord("goblin#0", new Dictionary<string, object> { ["itemId"] = "sword" }),
                new DataRecord("goblin#1", new Dictionary<string, object> { ["itemId"] = "sword" })
            };
            var wolfDrops = new List<DataRecord>
            {
                new DataRecord("wolf#0", new Dictionary<string, object> { ["itemId"] = "sword" })
            };

            var collection = BuildParentCollection(("goblin", goblinDrops), ("wolf", wolfDrops));
            var rule = new NoDuplicateRule("itemId", nestedField: "dropTable");

            var issues = rule.Evaluate(collection).ToList();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].RecordId, Is.EqualTo("goblin#1"));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: compile failure — `NoDuplicateRule` has no `nestedField` parameter yet.

- [ ] **Step 3: Implement nested-field support**

```csharp
// Package/Runtime/Scry.Core/Rules/NoDuplicateRule.cs
using System;
using System.Collections.Generic;

namespace Scry.Core.Rules
{
    public sealed class NoDuplicateRule : ValidationRule
    {
        private readonly string _field;
        private readonly string _nestedField;

        public NoDuplicateRule(string field, string nestedField = null)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _nestedField = nestedField;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            if (_nestedField == null)
            {
                foreach (var issue in EvaluateNoDuplicates(collection.Records))
                    yield return issue;
                yield break;
            }

            foreach (var parent in collection.Records)
            {
                var nested = parent.GetValue(_nestedField) as IReadOnlyList<DataRecord>;
                if (nested == null)
                    continue;

                foreach (var issue in EvaluateNoDuplicates(nested))
                    yield return issue;
            }
        }

        private IEnumerable<ValidationIssue> EvaluateNoDuplicates(IEnumerable<DataRecord> records)
        {
            var seen = new Dictionary<object, string>();

            foreach (var record in records)
            {
                var value = record.GetValue(_field);
                if (value == null)
                    continue;

                if (seen.TryGetValue(value, out var firstRecordId))
                {
                    yield return new ValidationIssue(
                        recordId: record.Id,
                        fieldName: _field,
                        message: $"Duplicate value '{value}' for '{_field}' (already used by record '{firstRecordId}').");
                }
                else
                {
                    seen[value] = record.Id;
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: all tests pass, including the new one and the 3 pre-existing `NoDuplicateRuleTests`.

- [ ] **Step 5: Commit**

```bash
git add Package/Runtime/Scry.Core/Rules/NoDuplicateRule.cs Package/Tests/Scry.Core.Tests/Rules/NoDuplicateRuleTests.cs
git commit -m "feat(core): add nestedField support to NoDuplicateRule"
```

---

### Task 4: `Core` — `RequiredAtLeastOnceRule` nested-field support

**Files:**
- Modify: `Package/Runtime/Scry.Core/Rules/RequiredAtLeastOnceRule.cs`
- Test: `Package/Tests/Scry.Core.Tests/Rules/RequiredAtLeastOnceRuleTests.cs`

**Interfaces:**
- Produces: `RequiredAtLeastOnceRule(string field, object requiredValue, string nestedField = null)`. When `nestedField` is set, each parent must have at least one nested entry matching; a parent with none yields an issue with `recordId` = the parent's `Id`.

- [ ] **Step 1: Write the failing test**

Append to `Package/Tests/Scry.Core.Tests/Rules/RequiredAtLeastOnceRuleTests.cs` (inside the existing class, after `Evaluate_ReportsIssue_WhenNoRecordMatches`):

```csharp
        private static DataCollection BuildParentCollection(params (string parentId, List<DataRecord> dropTable)[] parents)
        {
            var elementSchema = new Schema("DropEntry", new[] { new FieldDescriptor("rarity", FieldType.String) });
            var schema = new Schema("Monster", new[] { new FieldDescriptor("dropTable", FieldType.Collection, elementSchema) });
            var records = parents.Select(p => new DataRecord(p.parentId, new Dictionary<string, object>
            {
                ["dropTable"] = (IReadOnlyList<DataRecord>)p.dropTable
            }));
            return new DataCollection(schema, records);
        }

        [Test]
        public void Evaluate_NestedField_ReportsIssuePerParent_WhenNoNestedEntryMatches()
        {
            var goblinDrops = new List<DataRecord>
            {
                new DataRecord("goblin#0", new Dictionary<string, object> { ["rarity"] = "Common" })
            };
            var wolfDrops = new List<DataRecord>
            {
                new DataRecord("wolf#0", new Dictionary<string, object> { ["rarity"] = "Rare" })
            };

            var collection = BuildParentCollection(("goblin", goblinDrops), ("wolf", wolfDrops));
            var rule = new RequiredAtLeastOnceRule("rarity", "Rare", nestedField: "dropTable");

            var issues = rule.Evaluate(collection).ToList();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].RecordId, Is.EqualTo("goblin"));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: compile failure — `RequiredAtLeastOnceRule` has no `nestedField` parameter yet.

- [ ] **Step 3: Implement nested-field support**

```csharp
// Package/Runtime/Scry.Core/Rules/RequiredAtLeastOnceRule.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core.Rules
{
    public sealed class RequiredAtLeastOnceRule : ValidationRule
    {
        private readonly string _field;
        private readonly object _requiredValue;
        private readonly string _nestedField;

        public RequiredAtLeastOnceRule(string field, object requiredValue, string nestedField = null)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _requiredValue = requiredValue;
            _nestedField = nestedField;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            if (_nestedField == null)
            {
                var found = collection.Records.Any(r => Equals(r.GetValue(_field), _requiredValue));
                if (!found)
                {
                    yield return new ValidationIssue(
                        recordId: null,
                        fieldName: _field,
                        message: $"No record has '{_field}' equal to '{_requiredValue}'; at least one is required.");
                }
                yield break;
            }

            foreach (var parent in collection.Records)
            {
                var nested = parent.GetValue(_nestedField) as IReadOnlyList<DataRecord>;
                var found = nested != null && nested.Any(r => Equals(r.GetValue(_field), _requiredValue));

                if (!found)
                {
                    yield return new ValidationIssue(
                        recordId: parent.Id,
                        fieldName: _field,
                        message: $"Record '{parent.Id}' has no entry in '{_nestedField}' with '{_field}' equal to '{_requiredValue}'; at least one is required.");
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: all tests pass, including the new one and the 2 pre-existing `RequiredAtLeastOnceRuleTests`.

- [ ] **Step 5: Commit**

```bash
git add Package/Runtime/Scry.Core/Rules/RequiredAtLeastOnceRule.cs Package/Tests/Scry.Core.Tests/Rules/RequiredAtLeastOnceRuleTests.cs
git commit -m "feat(core): add nestedField support to RequiredAtLeastOnceRule"
```

---

### Task 5: `Core.Unity` — `SchemaMapper` Collection field detection

**Files:**
- Create: `Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestDropEntry.cs`
- Create: `Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestMonsterData.cs`
- Create: `Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestOuterEntry.cs`
- Create: `Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestNestedListData.cs`
- Modify: `Package/Editor/Scry.Core.Unity/SchemaMapper.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/SchemaMapperTests.cs`

**Interfaces:**
- Consumes: `FieldType.Collection`, `FieldDescriptor.ElementSchema` (Task 1).
- Produces: `SchemaMapper.InferSchema` now maps a `List<T>`/`T[]` field of a plain `[Serializable]` class `T` as `FieldType.Collection` with `ElementSchema = InferSchema(typeof(T))`. Nested collections within an `ElementSchema` (i.e. `List<List<T>>`-shaped data) stay `Unsupported`. Consumed by `ScriptableObjectRepository` (Task 6).

- [ ] **Step 1: Create the fixtures**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestDropEntry.cs
using System;

namespace Scry.Core.Unity.Tests.Fixtures
{
    [Serializable]
    public class TestDropEntry
    {
        public string itemId;
        public float weight;
    }
}
```

```csharp
// Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestMonsterData.cs
using System.Collections.Generic;
using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public class TestMonsterData : ScriptableObject
    {
        public string monsterName;
        public List<TestDropEntry> dropTable = new List<TestDropEntry>();
    }
}
```

```csharp
// Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestOuterEntry.cs
using System;
using System.Collections.Generic;

namespace Scry.Core.Unity.Tests.Fixtures
{
    [Serializable]
    public class TestOuterEntry
    {
        public List<TestDropEntry> inner;
    }
}
```

```csharp
// Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestNestedListData.cs
using System.Collections.Generic;
using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public class TestNestedListData : ScriptableObject
    {
        public List<TestOuterEntry> outer = new List<TestOuterEntry>();
    }
}
```

- [ ] **Step 2: Write the failing tests**

Append to `Package/Tests/Scry.Core.Unity.Tests/SchemaMapperTests.cs` (inside the existing class, after `InferSchema_IncludesPrivateSerializeFieldInheritedFromBaseClass`):

```csharp
        [Test]
        public void InferSchema_MapsListOfSerializableClass_AsCollectionWithElementSchema()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestMonsterData));

            var field = schema.GetField("dropTable");

            Assert.AreEqual(FieldType.Collection, field.Type);
            Assert.IsNotNull(field.ElementSchema);
            Assert.AreEqual(FieldType.String, field.ElementSchema.GetField("itemId").Type);
            Assert.AreEqual(FieldType.Numeric, field.ElementSchema.GetField("weight").Type);
        }

        [Test]
        public void InferSchema_DoesNotAllowNestedCollectionsWithinAnElementSchema()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestNestedListData));

            var field = schema.GetField("outer");

            Assert.AreEqual(FieldType.Collection, field.Type);
            Assert.AreEqual(FieldType.Unsupported, field.ElementSchema.GetField("inner").Type);
        }
```

- [ ] **Step 3: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: both new tests fail — `dropTable`/`outer` map to `Unsupported` (no `List<>` detection yet), so `field.ElementSchema` is null and the assertions fail rather than error, but the run should show 2 failed.

- [ ] **Step 4: Implement Collection detection**

```csharp
// Package/Editor/Scry.Core.Unity/SchemaMapper.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using Scry.Core;
using UnityEngine;

namespace Scry.Core.Unity
{
    public static class SchemaMapper
    {
        public static Schema InferSchema(Type scriptableObjectType)
        {
            if (scriptableObjectType == null)
                throw new ArgumentNullException(nameof(scriptableObjectType));

            return InferSchema(scriptableObjectType, allowCollectionFields: true);
        }

        private static Schema InferSchema(Type type, bool allowCollectionFields)
        {
            var seenNames = new HashSet<string>();
            var fields = new List<FieldDescriptor>();

            // Public fields, including those inherited from base classes — GetFields already walks
            // the hierarchy for BindingFlags.Public, no manual walk needed here.
            foreach (var member in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (seenNames.Add(member.Name))
                    fields.Add(MapField(member.Name, member.FieldType, allowCollectionFields));
            }

            // Non-public [SerializeField] fields. Unlike Public, BindingFlags.NonPublic only returns
            // fields declared directly on the queried type — it does NOT return private fields
            // declared on base types. Unity still serializes those, so walk the hierarchy ourselves.
            foreach (var member in CollectNonPublicSerializedFields(type))
            {
                if (seenNames.Add(member.Name))
                    fields.Add(MapField(member.Name, member.FieldType, allowCollectionFields));
            }

            return new Schema(type.Name, fields);
        }

        private static IEnumerable<FieldInfo> CollectNonPublicSerializedFields(Type type)
        {
            for (var current = type; current != null && current != typeof(UnityEngine.Object); current = current.BaseType)
            {
                foreach (var member in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (member.GetCustomAttribute<SerializeField>() != null)
                        yield return member;
                }
            }
        }

        private static FieldDescriptor MapField(string name, Type fieldType, bool allowCollectionFields)
        {
            if (allowCollectionFields)
            {
                var elementType = GetCollectionElementType(fieldType);
                if (elementType != null && IsPlainSerializableClass(elementType))
                {
                    // Nested collection schemas never allow further collection fields -
                    // List<List<T>> (or deeper) is out of scope and stays Unsupported instead.
                    var elementSchema = InferSchema(elementType, allowCollectionFields: false);
                    return new FieldDescriptor(name, FieldType.Collection, elementSchema);
                }
            }

            return new FieldDescriptor(name, MapFieldType(fieldType));
        }

        private static Type GetCollectionElementType(Type fieldType)
        {
            if (fieldType.IsArray)
                return fieldType.GetElementType();

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                return fieldType.GetGenericArguments()[0];

            return null;
        }

        private static bool IsPlainSerializableClass(Type type)
        {
            return type.IsClass
                && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                && type.IsDefined(typeof(SerializableAttribute), inherit: false);
        }

        private static FieldType MapFieldType(Type type)
        {
            if (type == typeof(int) || type == typeof(float))
                return FieldType.Numeric;
            if (type == typeof(string))
                return FieldType.String;
            if (type == typeof(bool))
                return FieldType.Boolean;
            if (type.IsEnum)
                return FieldType.Enum;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return FieldType.Reference;

            return FieldType.Unsupported;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 3.
Expected: exit code 0; `test_results.xml` shows all `SchemaMapperTests` passing, including the 2 new ones. Delete `test_results.xml` and `test_run.txt` after checking (scratch logs).

- [ ] **Step 6: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/SchemaMapper.cs Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestDropEntry.cs Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestMonsterData.cs Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestOuterEntry.cs Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestNestedListData.cs Package/Tests/Scry.Core.Unity.Tests/SchemaMapperTests.cs
git commit -m "feat(core.unity): detect List<T>/T[] of serializable classes as Collection fields"
```

---

### Task 6: `Core.Unity` — `ScriptableObjectRepository.Scan` reads Collection fields

**Files:**
- Modify: `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs`

**Interfaces:**
- Consumes: `SchemaMapper` Collection detection (Task 5).
- Produces: `Scan` now populates a `Collection` field's value as `List<DataRecord>` (assignable to `IReadOnlyList<DataRecord>`), one nested `DataRecord` per array element, id `"{parentGuid}#{index}"`. New private helper `ReadCollectionValue(SerializedProperty arrayProperty, Schema elementSchema, string parentRecordId)`, reused by Tasks 7-8.

- [ ] **Step 1: Write the failing tests**

Append to `Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs` (inside the existing class, after `ApplyEdit_ThrowsWriteConflict_WhenAssetChangedExternallySinceScan`):

```csharp
        [Test]
        public void Scan_ReadsCollectionField_AsNestedDataRecords()
        {
            var monster = ScriptableObject.CreateInstance<TestMonsterData>();
            monster.monsterName = "Goblin";
            monster.dropTable = new List<TestDropEntry>
            {
                new TestDropEntry { itemId = "sword", weight = 60f },
                new TestDropEntry { itemId = "shield", weight = 40f }
            };
            AssetDatabase.CreateAsset(monster, $"{FixtureFolder}/Goblin.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestMonsterData));

            var record = collection.Records[0];
            var dropTable = record.GetValue("dropTable") as IReadOnlyList<DataRecord>;

            Assert.IsNotNull(dropTable);
            Assert.AreEqual(2, dropTable.Count);
            Assert.AreEqual("sword", dropTable[0].GetValue("itemId"));
            Assert.AreEqual(60f, dropTable[0].GetValue("weight"));
            Assert.AreEqual($"{record.Id}#0", dropTable[0].Id);
        }

        [Test]
        public void Scan_ReadsEmptyCollectionField_AsEmptyList()
        {
            var monster = ScriptableObject.CreateInstance<TestMonsterData>();
            AssetDatabase.CreateAsset(monster, $"{FixtureFolder}/EmptyGoblin.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestMonsterData));

            var dropTable = collection.Records[0].GetValue("dropTable") as IReadOnlyList<DataRecord>;

            Assert.IsNotNull(dropTable);
            Assert.IsEmpty(dropTable);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: both new tests fail — `Scan` currently calls `ReadValue` for every field, which returns `null` for `FieldType.Collection` (unhandled `default` case), so `dropTable` is `null`, not a list.

- [ ] **Step 3: Implement Collection reading in `Scan`**

Modify `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`: replace the `Scan` method's field-reading loop and add `ReadCollectionValue`:

```csharp
        public DataCollection Scan(Type scriptableObjectType)
        {
            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var guids = AssetDatabase.FindAssets($"t:{scriptableObjectType.Name}");
            var records = new List<DataRecord>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
                if (asset == null)
                    continue;

                var serializedObject = new SerializedObject(asset);
                var values = new Dictionary<string, object>();

                foreach (var field in schema.Fields)
                {
                    var property = serializedObject.FindProperty(field.Name);
                    values[field.Name] = field.Type == FieldType.Collection
                        ? (object)ReadCollectionValue(property, field.ElementSchema, guid)
                        : ReadValue(property, field.Type);
                }

                var fingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
                records.Add(new DataRecord(guid, values, fingerprint));
            }

            return new DataCollection(schema, records);
        }

        private static List<DataRecord> ReadCollectionValue(SerializedProperty arrayProperty, Schema elementSchema, string parentRecordId)
        {
            var entries = new List<DataRecord>();
            if (arrayProperty == null || !arrayProperty.isArray)
                return entries;

            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                var elementProperty = arrayProperty.GetArrayElementAtIndex(i);
                var values = new Dictionary<string, object>();

                foreach (var field in elementSchema.Fields)
                {
                    var childProperty = elementProperty.FindPropertyRelative(field.Name);
                    values[field.Name] = ReadValue(childProperty, field.Type);
                }

                entries.Add(new DataRecord($"{parentRecordId}#{i}", values));
            }

            return entries;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 2.
Expected: exit code 0; `test_results.xml` shows all `ScriptableObjectRepositoryTests` passing, including the 2 new ones and the 7 pre-existing ones. Delete `test_results.xml` and `test_run.txt` after checking.

- [ ] **Step 5: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs
git commit -m "feat(core.unity): read Collection fields as nested DataRecords in Scan"
```

---

### Task 7: `Core.Unity` — `ApplyEdit` for a nested cell

**Files:**
- Modify: `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs`

**Interfaces:**
- Consumes: `ReadCollectionValue` (Task 6).
- Produces: new overload `ApplyEdit(DataRecord record, string collectionField, int index, string childFieldName, object value, Type scriptableObjectType)`. Also factors the existing 4-arg `ApplyEdit`'s fingerprint check into a reusable private helper `ResolveAndCheckFingerprint`, and adds `ResolveCollectionField`, both reused by Task 8.

- [ ] **Step 1: Write the failing tests**

Append to `Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs` (inside the existing class, after the Task 6 tests):

```csharp
        [Test]
        public void ApplyEdit_NestedField_WritesValueBackToArrayElement()
        {
            var monster = ScriptableObject.CreateInstance<TestMonsterData>();
            monster.dropTable = new List<TestDropEntry> { new TestDropEntry { itemId = "sword", weight = 60f } };
            var path = $"{FixtureFolder}/Goblin.asset";
            AssetDatabase.CreateAsset(monster, path);
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestMonsterData));
            var record = collection.Records[0];

            var updated = _repository.ApplyEdit(record, "dropTable", 0, "weight", 75f, typeof(TestMonsterData));

            var dropTable = updated.GetValue("dropTable") as IReadOnlyList<DataRecord>;
            Assert.AreEqual(75f, dropTable[0].GetValue("weight"));

            var reloaded = _repository.Scan(typeof(TestMonsterData));
            var reloadedDropTable = reloaded.Records[0].GetValue("dropTable") as IReadOnlyList<DataRecord>;
            Assert.AreEqual(75f, reloadedDropTable[0].GetValue("weight"));
        }

        [Test]
        public void ApplyEdit_NestedField_ThrowsWriteConflict_WhenAssetChangedExternallySinceScan()
        {
            var monster = ScriptableObject.CreateInstance<TestMonsterData>();
            monster.dropTable = new List<TestDropEntry> { new TestDropEntry { itemId = "sword", weight = 60f } };
            var path = $"{FixtureFolder}/Goblin.asset";
            AssetDatabase.CreateAsset(monster, path);
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestMonsterData));
            var record = collection.Records[0];

            var externallyLoaded = AssetDatabase.LoadAssetAtPath<TestMonsterData>(path);
            externallyLoaded.monsterName = "Changed";
            EditorUtility.SetDirty(externallyLoaded);
            AssetDatabase.SaveAssets();

            Assert.Throws<WriteConflictException>(() =>
                _repository.ApplyEdit(record, "dropTable", 0, "weight", 99f, typeof(TestMonsterData)));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: compile failure — no 6-argument `ApplyEdit` overload exists yet.

- [ ] **Step 3: Implement the nested `ApplyEdit` overload**

Modify `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`: replace the existing 4-arg `ApplyEdit` with the version below (behavior unchanged, just uses the new helpers), and add the new overload plus the two helpers, right after it:

```csharp
        public DataRecord ApplyEdit(DataRecord record, string fieldName, object value, Type scriptableObjectType)
        {
            var path = ResolveAndCheckFingerprint(record);

            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var fieldDescriptor = schema.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (fieldDescriptor == null)
                throw new InvalidOperationException($"Field '{fieldName}' is not part of the schema for '{scriptableObjectType.Name}'.");

            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
            if (asset == null)
                throw new InvalidOperationException($"Asset for record '{record.Id}' could not be loaded.");

            var serializedObject = new SerializedObject(asset);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found on asset '{path}'.");

            WriteValue(property, value);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var updatedValues = new Dictionary<string, object>(record.Values)
            {
                [fieldName] = ReadValue(property, fieldDescriptor.Type)
            };
            var freshFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return new DataRecord(record.Id, updatedValues, freshFingerprint);
        }

        public DataRecord ApplyEdit(DataRecord record, string collectionField, int index, string childFieldName, object value, Type scriptableObjectType)
        {
            var path = ResolveAndCheckFingerprint(record);

            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var fieldDescriptor = ResolveCollectionField(schema, collectionField);

            var childDescriptor = fieldDescriptor.ElementSchema.Fields.FirstOrDefault(f => f.Name == childFieldName);
            if (childDescriptor == null)
                throw new InvalidOperationException($"Field '{childFieldName}' is not part of the element schema for '{collectionField}'.");

            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
            if (asset == null)
                throw new InvalidOperationException($"Asset for record '{record.Id}' could not be loaded.");

            var serializedObject = new SerializedObject(asset);
            var arrayProperty = serializedObject.FindProperty(collectionField);
            var elementProperty = arrayProperty.GetArrayElementAtIndex(index);
            var childProperty = elementProperty.FindPropertyRelative(childFieldName);
            if (childProperty == null)
                throw new InvalidOperationException($"Field '{childFieldName}' not found on element {index} of '{collectionField}'.");

            WriteValue(childProperty, value);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var updatedValues = new Dictionary<string, object>(record.Values)
            {
                [collectionField] = ReadCollectionValue(serializedObject.FindProperty(collectionField), fieldDescriptor.ElementSchema, record.Id)
            };
            var freshFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return new DataRecord(record.Id, updatedValues, freshFingerprint);
        }

        private static string ResolveAndCheckFingerprint(DataRecord record)
        {
            if (record.Fingerprint == null)
                throw new ArgumentException("record.Fingerprint is null; conflict detection requires a record produced by Scan.", nameof(record));

            var path = AssetDatabase.GUIDToAssetPath(record.Id);
            var currentFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            if (currentFingerprint != record.Fingerprint)
                throw new WriteConflictException(record.Id);

            return path;
        }

        private static FieldDescriptor ResolveCollectionField(Schema schema, string collectionFieldName)
        {
            var fieldDescriptor = schema.Fields.FirstOrDefault(f => f.Name == collectionFieldName);
            if (fieldDescriptor == null || fieldDescriptor.Type != FieldType.Collection)
                throw new InvalidOperationException($"Field '{collectionFieldName}' is not a Collection field.");

            return fieldDescriptor;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 2.
Expected: exit code 0; `test_results.xml` shows all `ScriptableObjectRepositoryTests` passing, including the 2 new ones and every pre-existing test (the 4-arg `ApplyEdit` tests must still pass unchanged, since Step 3 only refactored its internals, not its behavior). Delete `test_results.xml` and `test_run.txt` after checking.

- [ ] **Step 5: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs
git commit -m "feat(core.unity): add ApplyEdit overload for nested collection cells"
```

---

### Task 8: `Core.Unity` — `AddCollectionEntry` / `RemoveCollectionEntry`

**Files:**
- Modify: `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs`

**Interfaces:**
- Consumes: `ResolveAndCheckFingerprint`, `ResolveCollectionField`, `ReadCollectionValue` (Task 7/6).
- Produces: `AddCollectionEntry(DataRecord record, string collectionField, Type scriptableObjectType)` and `RemoveCollectionEntry(DataRecord record, string collectionField, int index, Type scriptableObjectType)`, both returning the updated top-level `DataRecord` with a fresh fingerprint. These are the row add/remove operations the UI's sub-table expansion will call in the follow-up plan.

- [ ] **Step 1: Write the failing tests**

Append to `Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs` (inside the existing class, after the Task 7 tests):

```csharp
        [Test]
        public void AddCollectionEntry_AppendsDefaultEntry_NotDuplicatingLastRow()
        {
            var monster = ScriptableObject.CreateInstance<TestMonsterData>();
            monster.dropTable = new List<TestDropEntry> { new TestDropEntry { itemId = "sword", weight = 60f } };
            var path = $"{FixtureFolder}/Goblin.asset";
            AssetDatabase.CreateAsset(monster, path);
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestMonsterData));
            var record = collection.Records[0];

            var updated = _repository.AddCollectionEntry(record, "dropTable", typeof(TestMonsterData));

            var dropTable = updated.GetValue("dropTable") as IReadOnlyList<DataRecord>;
            Assert.AreEqual(2, dropTable.Count);
            Assert.AreEqual(string.Empty, dropTable[1].GetValue("itemId"));
            Assert.AreEqual(0f, dropTable[1].GetValue("weight"));
        }

        [Test]
        public void RemoveCollectionEntry_RemovesElementAtIndex()
        {
            var monster = ScriptableObject.CreateInstance<TestMonsterData>();
            monster.dropTable = new List<TestDropEntry>
            {
                new TestDropEntry { itemId = "sword", weight = 60f },
                new TestDropEntry { itemId = "shield", weight = 40f }
            };
            var path = $"{FixtureFolder}/Goblin.asset";
            AssetDatabase.CreateAsset(monster, path);
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestMonsterData));
            var record = collection.Records[0];

            var updated = _repository.RemoveCollectionEntry(record, "dropTable", 0, typeof(TestMonsterData));

            var dropTable = updated.GetValue("dropTable") as IReadOnlyList<DataRecord>;
            Assert.AreEqual(1, dropTable.Count);
            Assert.AreEqual("shield", dropTable[0].GetValue("itemId"));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: compile failure — `AddCollectionEntry`/`RemoveCollectionEntry` don't exist yet.

- [ ] **Step 3: Implement `AddCollectionEntry`, `RemoveCollectionEntry`, and `ResetToDefault`**

Add to `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`, after the helpers added in Task 7:

```csharp
        public DataRecord AddCollectionEntry(DataRecord record, string collectionField, Type scriptableObjectType)
        {
            var path = ResolveAndCheckFingerprint(record);

            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var fieldDescriptor = ResolveCollectionField(schema, collectionField);

            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
            if (asset == null)
                throw new InvalidOperationException($"Asset for record '{record.Id}' could not be loaded.");

            var serializedObject = new SerializedObject(asset);
            var arrayProperty = serializedObject.FindProperty(collectionField);
            var newIndex = arrayProperty.arraySize;
            arrayProperty.InsertArrayElementAtIndex(newIndex);

            // InsertArrayElementAtIndex duplicates the previous last element's values on a
            // non-empty array, rather than inserting a blank one - reset each field explicitly
            // so a newly added row starts empty instead of cloning the row above it.
            var newElement = arrayProperty.GetArrayElementAtIndex(newIndex);
            foreach (var elementField in fieldDescriptor.ElementSchema.Fields.Where(f => f.IsSupported))
                ResetToDefault(newElement.FindPropertyRelative(elementField.Name));

            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var updatedValues = new Dictionary<string, object>(record.Values)
            {
                [collectionField] = ReadCollectionValue(serializedObject.FindProperty(collectionField), fieldDescriptor.ElementSchema, record.Id)
            };
            var freshFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return new DataRecord(record.Id, updatedValues, freshFingerprint);
        }

        public DataRecord RemoveCollectionEntry(DataRecord record, string collectionField, int index, Type scriptableObjectType)
        {
            var path = ResolveAndCheckFingerprint(record);

            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var fieldDescriptor = ResolveCollectionField(schema, collectionField);

            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
            if (asset == null)
                throw new InvalidOperationException($"Asset for record '{record.Id}' could not be loaded.");

            var serializedObject = new SerializedObject(asset);
            var arrayProperty = serializedObject.FindProperty(collectionField);

            // A plain [Serializable] array element (not an object reference or managed reference) is
            // removed by a single DeleteArrayElementAtIndex call - the "call it twice" caveat in Unity's
            // docs only applies to object-reference array elements, which Collection fields never are.
            arrayProperty.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var updatedValues = new Dictionary<string, object>(record.Values)
            {
                [collectionField] = ReadCollectionValue(serializedObject.FindProperty(collectionField), fieldDescriptor.ElementSchema, record.Id)
            };
            var freshFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return new DataRecord(record.Id, updatedValues, freshFingerprint);
        }

        private static void ResetToDefault(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = 0;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = false;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported property type '{property.propertyType}'.");
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 2.
Expected: exit code 0; `test_results.xml` shows all `ScriptableObjectRepositoryTests` passing, including the 2 new ones. Delete `test_results.xml` and `test_run.txt` after checking.

- [ ] **Step 5: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs
git commit -m "feat(core.unity): add AddCollectionEntry and RemoveCollectionEntry"
```

---

### Task 9: `Core.Unity` — `[ScryCollection]` attribute

**Files:**
- Create: `Package/Editor/Scry.Core.Unity/ScryCollectionAttribute.cs`
- Create: `Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestScryCollectionAttributedData.cs`

No test file for this task — a bare marker attribute with no behavior has nothing to assert beyond "it exists and applies to a class," which Task 13's `ScryConfigSyncTests` already exercises against a fixture carrying it. This task only needs to compile.

**Interfaces:**
- Produces: `[ScryCollectionAttribute]`, an `[AttributeUsage(AttributeTargets.Class, Inherited = false)]` marker. Consumed by `ScryConfigSync` (Task 13).

- [ ] **Step 1: Implement the attribute**

```csharp
// Package/Editor/Scry.Core.Unity/ScryCollectionAttribute.cs
using System;

namespace Scry.Core.Unity
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ScryCollectionAttribute : Attribute
    {
    }
}
```

- [ ] **Step 2: Add a fixture type carrying it, for Task 13's tests to discover**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestScryCollectionAttributedData.cs
using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    [ScryCollection]
    public class TestScryCollectionAttributedData : ScriptableObject
    {
        public string label;
    }
}
```

- [ ] **Step 3: Verify compilation**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\compile_check.txt"
```

Expected: exit code 0, log ends with `Exiting batchmode successfully now!`. Delete `compile_check.txt` after checking.

- [ ] **Step 4: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/ScryCollectionAttribute.cs Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestScryCollectionAttributedData.cs
git commit -m "feat(core.unity): add [ScryCollection] discovery attribute"
```

---

### Task 10: `Core.Unity` — `RuleConfig` base and concrete rule configs

**Files:**
- Create: `Package/Editor/Scry.Core.Unity/Config/RuleConfig.cs`
- Create: `Package/Editor/Scry.Core.Unity/Config/SumEqualsRuleConfig.cs`
- Create: `Package/Editor/Scry.Core.Unity/Config/NoDuplicateRuleConfig.cs`
- Create: `Package/Editor/Scry.Core.Unity/Config/RequiredAtLeastOnceRuleConfig.cs`

No test file — these are plain serializable data-holder classes with no logic; their round-trip through Unity serialization is covered by Task 12's `ScryConfigTests`, and their conversion to real `ValidationRule`s by Task 11's `RuleFactoryTests`.

**Interfaces:**
- Produces: `abstract class RuleConfig` and its three concrete subclasses, each with public fields matching the corresponding `Core.Rules` constructor's parameter names. Consumed by `TrackedCollection`/`ScryConfig` (Task 12) and `RuleFactory` (Task 11).

- [ ] **Step 1: Implement the abstract base**

```csharp
// Package/Editor/Scry.Core.Unity/Config/RuleConfig.cs
using System;

namespace Scry.Core.Unity.Config
{
    [Serializable]
    public abstract class RuleConfig
    {
    }
}
```

- [ ] **Step 2: Implement the three concrete configs**

```csharp
// Package/Editor/Scry.Core.Unity/Config/SumEqualsRuleConfig.cs
using System;

namespace Scry.Core.Unity.Config
{
    [Serializable]
    public sealed class SumEqualsRuleConfig : RuleConfig
    {
        public string Field;
        public double Target;
        public double Tolerance = 0.0001;
        public string GroupByField;
        public string NestedField;
    }
}
```

```csharp
// Package/Editor/Scry.Core.Unity/Config/NoDuplicateRuleConfig.cs
using System;

namespace Scry.Core.Unity.Config
{
    [Serializable]
    public sealed class NoDuplicateRuleConfig : RuleConfig
    {
        public string Field;
        public string NestedField;
    }
}
```

```csharp
// Package/Editor/Scry.Core.Unity/Config/RequiredAtLeastOnceRuleConfig.cs
using System;

namespace Scry.Core.Unity.Config
{
    [Serializable]
    public sealed class RequiredAtLeastOnceRuleConfig : RuleConfig
    {
        public string Field;

        // Compared via Equals against the target field's raw value, so this matches
        // String-typed fields in v1. Enum/Numeric target support needs RuleFactory to see
        // the target Schema at build time, which it doesn't yet - deferred, not forgotten.
        public string RequiredValue;
        public string NestedField;
    }
}
```

- [ ] **Step 3: Verify compilation**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\compile_check.txt"
```

Expected: exit code 0, log ends with `Exiting batchmode successfully now!`. Delete `compile_check.txt` after checking.

- [ ] **Step 4: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/Config/RuleConfig.cs Package/Editor/Scry.Core.Unity/Config/SumEqualsRuleConfig.cs Package/Editor/Scry.Core.Unity/Config/NoDuplicateRuleConfig.cs Package/Editor/Scry.Core.Unity/Config/RequiredAtLeastOnceRuleConfig.cs
git commit -m "feat(core.unity): add RuleConfig base and concrete rule config classes"
```

---

### Task 11: `Core.Unity` — `RuleFactory`

**Files:**
- Create: `Package/Editor/Scry.Core.Unity/RuleFactory.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/RuleFactoryTests.cs`

**Interfaces:**
- Consumes: `RuleConfig` subclasses (Task 10), `Scry.Core.Rules.*` (Tasks 2-4).
- Produces: `static class RuleFactory { static ValidationRule Build(RuleConfig config); }`. Consumed by the UI layer's validation-running code in the follow-up plan.

- [ ] **Step 1: Write the failing tests**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/RuleFactoryTests.cs
using NUnit.Framework;
using Scry.Core;
using Scry.Core.Rules;
using Scry.Core.Unity.Config;

namespace Scry.Core.Unity.Tests
{
    public class RuleFactoryTests
    {
        [Test]
        public void Build_SumEqualsRuleConfig_ProducesWorkingRule()
        {
            var rule = RuleFactory.Build(new SumEqualsRuleConfig { Field = "weight", Target = 100, GroupByField = "table" });

            Assert.IsInstanceOf<SumEqualsRule>(rule);
        }

        [Test]
        public void Build_NoDuplicateRuleConfig_ProducesWorkingRule()
        {
            var rule = RuleFactory.Build(new NoDuplicateRuleConfig { Field = "itemId" });

            Assert.IsInstanceOf<NoDuplicateRule>(rule);
        }

        [Test]
        public void Build_RequiredAtLeastOnceRuleConfig_ProducesWorkingRule()
        {
            var rule = RuleFactory.Build(new RequiredAtLeastOnceRuleConfig { Field = "rarity", RequiredValue = "Starter" });

            Assert.IsInstanceOf<RequiredAtLeastOnceRule>(rule);
        }

        [Test]
        public void Build_UnknownRuleConfig_Throws()
        {
            Assert.Throws<System.NotSupportedException>(() => RuleFactory.Build(new UnknownRuleConfig()));
        }

        private sealed class UnknownRuleConfig : RuleConfig
        {
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: compile failure — `RuleFactory` doesn't exist yet.

- [ ] **Step 3: Implement `RuleFactory`**

```csharp
// Package/Editor/Scry.Core.Unity/RuleFactory.cs
using System;
using Scry.Core;
using Scry.Core.Rules;
using Scry.Core.Unity.Config;

namespace Scry.Core.Unity
{
    public static class RuleFactory
    {
        public static ValidationRule Build(RuleConfig config)
        {
            switch (config)
            {
                case SumEqualsRuleConfig c:
                    return new SumEqualsRule(c.Field, c.Target, c.Tolerance, c.GroupByField, c.NestedField);
                case NoDuplicateRuleConfig c:
                    return new NoDuplicateRule(c.Field, c.NestedField);
                case RequiredAtLeastOnceRuleConfig c:
                    return new RequiredAtLeastOnceRule(c.Field, c.RequiredValue, c.NestedField);
                default:
                    throw new NotSupportedException($"Unknown rule config type '{config?.GetType().Name ?? "null"}'.");
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 2.
Expected: exit code 0; `test_results.xml` shows all 4 `RuleFactoryTests` passing. Delete `test_results.xml` and `test_run.txt` after checking.

- [ ] **Step 5: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/RuleFactory.cs Package/Tests/Scry.Core.Unity.Tests/RuleFactoryTests.cs
git commit -m "feat(core.unity): add RuleFactory to build ValidationRules from RuleConfig"
```

---

### Task 12: `Core.Unity` — `TrackedCollection` and `ScryConfig`

**Files:**
- Create: `Package/Editor/Scry.Core.Unity/Config/TrackedCollection.cs`
- Create: `Package/Editor/Scry.Core.Unity/ScryConfig.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/ScryConfigTests.cs`

**Interfaces:**
- Consumes: `RuleConfig` (Task 10).
- Produces: `ScryConfig : ScriptableObject { IReadOnlyList<TrackedCollection> TrackedCollections; }`; `TrackedCollection { string TypeName; IReadOnlyList<RuleConfig> Rules; }`. Both are read-only from C# — mutation happens via `SerializedProperty` (editor code writes `trackedCollections`/`rules` directly), consistent with how `ScriptableObjectRepository.ApplyEdit` already treats `SerializedProperty` as the write path rather than adding field setters. Consumed by `ScryConfigSync` (Task 13) and the UI layer in the follow-up plan.

- [ ] **Step 1: Write the failing test**

This test is the riskiest check in this plan: it verifies `[SerializeReference]` actually preserves which concrete `RuleConfig` subclass was stored, and its field values, through a real Unity asset save + reload — not just in the same in-memory C# object, which would trivially "pass" without exercising serialization at all.

```csharp
// Package/Tests/Scry.Core.Unity.Tests/ScryConfigTests.cs
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Scry.Core.Unity.Config;

namespace Scry.Core.Unity.Tests
{
    public class ScryConfigTests
    {
        private const string FixtureFolder = "Assets/ScryTestFixtures";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
                AssetDatabase.CreateFolder("Assets", "ScryTestFixtures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }

        [Test]
        public void ScryConfig_RoundTripsSerializeReferenceRuleConfigs_ThroughAssetSaveAndReload()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            var path = $"{FixtureFolder}/TestConfig.asset";
            AssetDatabase.CreateAsset(config, path);

            var serializedObject = new SerializedObject(config);
            var tracked = serializedObject.FindProperty("trackedCollections");
            tracked.InsertArrayElementAtIndex(0);
            var trackedElement = tracked.GetArrayElementAtIndex(0);
            trackedElement.FindPropertyRelative("typeName").stringValue = "TestMonsterData";
            var rules = trackedElement.FindPropertyRelative("rules");
            rules.InsertArrayElementAtIndex(0);
            var ruleElement = rules.GetArrayElementAtIndex(0);
            ruleElement.managedReferenceValue = new SumEqualsRuleConfig { Field = "weight", Target = 100, GroupByField = "table" };
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reloaded = AssetDatabase.LoadAssetAtPath<ScryConfig>(path);
            var reloadedSerializedObject = new SerializedObject(reloaded);
            var reloadedRule = reloadedSerializedObject.FindProperty("trackedCollections")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("rules")
                .GetArrayElementAtIndex(0)
                .managedReferenceValue;

            Assert.IsInstanceOf<SumEqualsRuleConfig>(reloadedRule);
            var reloadedSumEquals = (SumEqualsRuleConfig)reloadedRule;
            Assert.AreEqual("weight", reloadedSumEquals.Field);
            Assert.AreEqual(100, reloadedSumEquals.Target);
            Assert.AreEqual("table", reloadedSumEquals.GroupByField);
        }

        [Test]
        public void ScryConfig_TrackedCollectionsExposesTypeNameAndRules_ViaPublicApi()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            var path = $"{FixtureFolder}/TestConfig2.asset";
            AssetDatabase.CreateAsset(config, path);

            var serializedObject = new SerializedObject(config);
            var tracked = serializedObject.FindProperty("trackedCollections");
            tracked.InsertArrayElementAtIndex(0);
            tracked.GetArrayElementAtIndex(0).FindPropertyRelative("typeName").stringValue = "TestMonsterData";
            serializedObject.ApplyModifiedProperties();

            Assert.AreEqual(1, config.TrackedCollections.Count);
            Assert.AreEqual("TestMonsterData", config.TrackedCollections[0].TypeName);
            Assert.IsEmpty(config.TrackedCollections[0].Rules);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: compile failure — `ScryConfig`/`TrackedCollection` don't exist yet.

- [ ] **Step 3: Implement `TrackedCollection` and `ScryConfig`**

```csharp
// Package/Editor/Scry.Core.Unity/Config/TrackedCollection.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry.Core.Unity.Config
{
    [Serializable]
    public sealed class TrackedCollection
    {
        [SerializeField] private string typeName;
        [SerializeReference] private List<RuleConfig> rules = new List<RuleConfig>();

        public string TypeName => typeName;
        public IReadOnlyList<RuleConfig> Rules => rules;
    }
}
```

```csharp
// Package/Editor/Scry.Core.Unity/ScryConfig.cs
using System.Collections.Generic;
using Scry.Core.Unity.Config;
using UnityEngine;

namespace Scry.Core.Unity
{
    [CreateAssetMenu(menuName = "Scry/Config", fileName = "ScryConfig")]
    public sealed class ScryConfig : ScriptableObject
    {
        [SerializeField] private List<TrackedCollection> trackedCollections = new List<TrackedCollection>();

        public IReadOnlyList<TrackedCollection> TrackedCollections => trackedCollections;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 2.
Expected: exit code 0; `test_results.xml` shows both `ScryConfigTests` passing. Delete `test_results.xml` and `test_run.txt` after checking.

- [ ] **Step 5: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/Config/TrackedCollection.cs Package/Editor/Scry.Core.Unity/ScryConfig.cs Package/Tests/Scry.Core.Unity.Tests/ScryConfigTests.cs
git commit -m "feat(core.unity): add ScryConfig and TrackedCollection asset model"
```

---

### Task 13: `Core.Unity` — `ScryConfigSync` and its Inspector button

**Files:**
- Create: `Package/Editor/Scry.Core.Unity/ScryConfigSync.cs`
- Create: `Package/Editor/Scry.Core.Unity/ScryConfigEditor.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/ScryConfigSyncTests.cs`

**Interfaces:**
- Consumes: `[ScryCollection]` (Task 9), `ScryConfig`/`TrackedCollection` (Task 12).
- Produces: `ScryConfigSync.GetMissingTrackedTypes(IReadOnlyList<TrackedCollection> existing) -> IEnumerable<Type>` (pure, testable) and `ScryConfigSync.SyncTrackedTypes(SerializedObject configSerializedObject)` (writes new `TrackedCollection` entries via `SerializedProperty`). `ScryConfigEditor` is a thin `[CustomEditor(typeof(ScryConfig))]` wrapper with one button calling `SyncTrackedTypes` — GUI code, manual-smoke-test only, per the architecture doc's stance that Editor GUI automation isn't attempted; the logic behind the button is what Task 13's tests cover.

- [ ] **Step 1: Write the failing tests**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/ScryConfigSyncTests.cs
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Scry.Core.Unity.Tests.Fixtures;

namespace Scry.Core.Unity.Tests
{
    public class ScryConfigSyncTests
    {
        private const string FixtureFolder = "Assets/ScryTestFixtures";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
                AssetDatabase.CreateFolder("Assets", "ScryTestFixtures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }

        [Test]
        public void GetMissingTrackedTypes_ReturnsAttributedType_WhenNotAlreadyTracked()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            AssetDatabase.CreateAsset(config, $"{FixtureFolder}/Config.asset");

            var missing = ScryConfigSync.GetMissingTrackedTypes(config.TrackedCollections).ToList();

            Assert.Contains(typeof(TestScryCollectionAttributedData), missing);
        }

        [Test]
        public void SyncTrackedTypes_AddsMissingAttributedTypes_AsTrackedCollections()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            var path = $"{FixtureFolder}/Config.asset";
            AssetDatabase.CreateAsset(config, path);

            var serializedObject = new SerializedObject(config);
            ScryConfigSync.SyncTrackedTypes(serializedObject);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var reloaded = AssetDatabase.LoadAssetAtPath<ScryConfig>(path);

            Assert.IsTrue(reloaded.TrackedCollections.Any(t => t.TypeName == typeof(TestScryCollectionAttributedData).AssemblyQualifiedName));
        }

        [Test]
        public void SyncTrackedTypes_DoesNotDuplicate_WhenTypeAlreadyTracked()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            var path = $"{FixtureFolder}/Config.asset";
            AssetDatabase.CreateAsset(config, path);

            var serializedObject = new SerializedObject(config);
            ScryConfigSync.SyncTrackedTypes(serializedObject);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var secondPass = new SerializedObject(config);
            ScryConfigSync.SyncTrackedTypes(secondPass);
            secondPass.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var reloaded = AssetDatabase.LoadAssetAtPath<ScryConfig>(path);
            var matches = reloaded.TrackedCollections.Count(t => t.TypeName == typeof(TestScryCollectionAttributedData).AssemblyQualifiedName);

            Assert.AreEqual(1, matches);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: compile failure — `ScryConfigSync` doesn't exist yet.

- [ ] **Step 3: Implement `ScryConfigSync` and `ScryConfigEditor`**

```csharp
// Package/Editor/Scry.Core.Unity/ScryConfigSync.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Scry.Core.Unity.Config;
using UnityEditor;

namespace Scry.Core.Unity
{
    public static class ScryConfigSync
    {
        public static IEnumerable<Type> GetMissingTrackedTypes(IReadOnlyList<TrackedCollection> existing)
        {
            var existingTypeNames = new HashSet<string>(existing.Select(t => t.TypeName));

            foreach (var type in TypeCache.GetTypesWithAttribute<ScryCollectionAttribute>())
            {
                if (!existingTypeNames.Contains(type.AssemblyQualifiedName))
                    yield return type;
            }
        }

        public static void SyncTrackedTypes(SerializedObject configSerializedObject)
        {
            var config = (ScryConfig)configSerializedObject.targetObject;
            var missing = GetMissingTrackedTypes(config.TrackedCollections).ToList();
            if (missing.Count == 0)
                return;

            var trackedProperty = configSerializedObject.FindProperty("trackedCollections");
            foreach (var type in missing)
            {
                var index = trackedProperty.arraySize;
                trackedProperty.InsertArrayElementAtIndex(index);
                trackedProperty.GetArrayElementAtIndex(index).FindPropertyRelative("typeName").stringValue = type.AssemblyQualifiedName;
            }
        }
    }
}
```

```csharp
// Package/Editor/Scry.Core.Unity/ScryConfigEditor.cs
using UnityEditor;
using UnityEngine;

namespace Scry.Core.Unity
{
    [CustomEditor(typeof(ScryConfig))]
    public sealed class ScryConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Sync Tracked Types"))
            {
                ScryConfigSync.SyncTrackedTypes(serializedObject);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 2.
Expected: exit code 0; `test_results.xml` shows all 3 `ScryConfigSyncTests` passing. Delete `test_results.xml` and `test_run.txt` after checking.

- [ ] **Step 5: Manual smoke test of the Inspector button**

Open the Unity Editor on `TestProject`, create a `ScryConfig` asset (`Assets > Create > Scry > Config`, per the `[CreateAssetMenu]` added on `ScryConfig` in Task 12), select it, and click "Sync Tracked Types" in the Inspector. Confirm a `TrackedCollection` entry appears for `TestScryCollectionAttributedData` (visible in the default Inspector as a `List` element under `Tracked Collections`). Delete the manually-created test asset afterward — it's scratch, not a fixture.

- [ ] **Step 6: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/ScryConfigSync.cs Package/Editor/Scry.Core.Unity/ScryConfigEditor.cs Package/Editor/Scry.Core.Unity/ScryConfig.cs Package/Tests/Scry.Core.Unity.Tests/ScryConfigSyncTests.cs
git commit -m "feat(core.unity): add ScryConfigSync and Inspector button for attribute-based discovery"
```

---

## Summary

After Task 13, `Core`/`Core.Unity` fully support scanning, editing, adding, removing, and validating `Collection` fields against real `ScriptableObject` assets, and a project can declare its tracked types and validation rules entirely as data via a `ScryConfig` asset — all exercised by `dotnet test`/Unity EditMode tests with no UI. The follow-up plan builds `DataGridWindow` and the `MultiColumnTreeView` grid (spec Section 4) on top of this.
