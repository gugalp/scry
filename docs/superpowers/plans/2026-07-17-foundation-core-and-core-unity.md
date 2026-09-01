# Foundation: Core + Core.Unity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the `Core` (plain .NET data model/validation) and `Core.Unity` (Editor-only Unity bridge) assemblies described in the architecture spec, with the scan → map → validate write-back pipeline fully working and tested — no UI yet. This is the shared foundation both Pillar 1 (data editor) and Pillar 2 (simulation) build on.

**Architecture:** Two assembly-definition-separated modules. `Core` is a plain C# class library with zero `UnityEngine`/`UnityEditor` reference (compiler-enforced via `asmdef`'s `noEngineReferences: true`), holding `Schema`/`FieldDescriptor`, `DataCollection`/`DataRecord`, and composable `ValidationRule`s. `Core.Unity` is an Editor-only assembly that maps real `ScriptableObject` assets into `Core`'s generic model (`SchemaMapper`, `ScriptableObjectRepository`) and exposes a headless batch-mode entry point. A minimal throwaway Unity project (`TestProject/`) is checked into this repo purely as a compilation/EditMode-test harness for the package — it is not a consumer of the package in the product sense, just CI-in-a-box until real Unity package CI tooling is set up.

**Tech Stack:** C# / .NET (netstandard2.1 for `Core`, Editor-only for `Core.Unity`), NUnit for both plain `dotnet test` (`Core`) and Unity Test Framework EditMode tests (`Core.Unity`), Unity 6000.3.10f1.

## Global Constraints

- Target Unity 6 (6000.x); the `TestProject` harness is pinned to 6000.3.10f1, the editor already installed and used by the dogfooding target (Adventure Dreams).
- C#/.NET throughout. `Core` is plain .NET with **zero** `UnityEngine`/`UnityEditor` reference — enforced by `Scry.Core.asmdef`'s `"noEngineReferences": true`, not by convention.
- `Core.Unity` is Editor-only (`"includePlatforms": ["Editor"]`) — it is the only assembly that touches Unity's asset APIs.
- Standalone UPM package: this repo is never embedded inside a consuming project (not Adventure Dreams, not anything else).
- Every `Core`/`Core.Unity` operation must be callable headlessly, with no interactive Editor window, from v1 onward.
- Nothing may be hardcoded to Adventure Dreams' vocabulary (rarity tiers, elite/boss multipliers, creature types) — `Schema`, `FieldDescriptor`, and `ValidationRule` primitives stay fully generic.
- Mapping failures (a field type `SchemaMapper` doesn't recognize) degrade gracefully: mark the field `Unsupported` and keep scanning — never throw or abort the whole scan.
- Write-back conflicts (asset changed externally since it was scanned) must be detected before applying and raised as a distinct exception — never silently overwritten.
- License and exact UPM distribution mechanism (git URL vs. OpenUPM) are open questions the spec explicitly defers — this plan does not resolve them (`package.json` omits a `license` field rather than guessing one).

---

## File Structure

```
scry/
  package.json                                  # UPM manifest for the whole tool
  .gitignore
  Runtime/
    Scry.Core/
      Scry.Core.asmdef                          # noEngineReferences: true
      Scry.Core.csproj                          # netstandard2.1, dotnet-testable in isolation
      FieldType.cs
      FieldDescriptor.cs
      Schema.cs
      DataRecord.cs
      DataCollection.cs
      ValidationSeverity.cs
      ValidationIssue.cs
      ValidationRule.cs
      Rules/
        SumEqualsRule.cs
        NoDuplicateRule.cs
        RequiredAtLeastOnceRule.cs
  Editor/
    Scry.Core.Unity/
      Scry.Core.Unity.asmdef                    # includePlatforms: ["Editor"], references Scry.Core
      SchemaMapper.cs
      ScriptableObjectRepository.cs
      WriteConflictException.cs
      BatchEntryPoint.cs
  Tests/
    Scry.Core.Tests/
      Scry.Core.Tests.csproj                    # dotnet test, no Unity required
      SmokeTests.cs
      FieldDescriptorTests.cs
      SchemaTests.cs
      DataCollectionTests.cs
      Rules/
        SumEqualsRuleTests.cs
        NoDuplicateRuleTests.cs
        RequiredAtLeastOnceRuleTests.cs
    Scry.Core.Unity.Tests/
      Scry.Core.Unity.Tests.asmdef               # Editor-only, Unity Test Framework
      Fixtures/
        TestItemData.cs
      SchemaMapperTests.cs
      ScriptableObjectRepositoryTests.cs
      BatchEntryPointTests.cs
  TestProject/                                   # throwaway Unity project: compiles/tests the package
    Assets/
      .gitkeep
    Packages/
      manifest.json                              # "com.scry.tool": "file:.."
    ProjectSettings/
      ProjectVersion.txt                         # pinned to 6000.3.10f1
```

`Runtime/Scry.Core` carries both a `.asmdef` (for Unity) and a hand-written `.csproj` (for plain `dotnet test`) pointed at the same `.cs` files — this is what makes `Core` "runnable via `dotnet test`, no Unity install required" per the spec, while still being compiled by Unity as part of the package.

---

### Task 1: Package & solution scaffolding

**Files:**
- Create: `package.json`
- Create: `.gitignore`
- Create: `Runtime/Scry.Core/Scry.Core.asmdef`
- Create: `Runtime/Scry.Core/Scry.Core.csproj`
- Create: `Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
- Create: `Tests/Scry.Core.Tests/SmokeTests.cs`
- Create: `Editor/Scry.Core.Unity/Scry.Core.Unity.asmdef`
- Create: `Tests/Scry.Core.Unity.Tests/Scry.Core.Unity.Tests.asmdef`
- Create: `TestProject/Assets/.gitkeep`
- Create: `TestProject/Packages/manifest.json`
- Create: `TestProject/ProjectSettings/ProjectVersion.txt`

**Interfaces:**
- Produces: assembly names `Scry.Core` (no Unity refs) and `Scry.Core.Unity` (Editor-only, references `Scry.Core`) that every later task's `.asmdef` builds on. Produces the `TestProject` harness used by every later task's "verify Unity compiles" step.

- [ ] **Step 1: Create `package.json`**

```json
{
  "name": "com.scry.tool",
  "version": "0.1.0",
  "displayName": "Scry",
  "description": "Browse, edit, and validate ScriptableObject-driven game data as a database, and simulate the balance it produces.",
  "unity": "6000.0",
  "keywords": ["scriptableobject", "data", "editor", "validation", "simulation"],
  "author": {
    "name": "Guga"
  }
}
```

- [ ] **Step 2: Create `.gitignore`**

```gitignore
# Unity (TestProject compilation/test harness)
TestProject/Library/
TestProject/Temp/
TestProject/Logs/
TestProject/obj/
TestProject/UserSettings/
TestProject/.vs/
TestProject/*.csproj
TestProject/*.sln
TestProject/Packages/packages-lock.json

# dotnet
[Bb]in/
[Oo]bj/
*.user

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 3: Create `Runtime/Scry.Core/Scry.Core.asmdef`**

```json
{
    "name": "Scry.Core",
    "rootNamespace": "Scry.Core",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

- [ ] **Step 4: Create `Runtime/Scry.Core/Scry.Core.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <RootNamespace>Scry.Core</RootNamespace>
    <AssemblyName>Scry.Core</AssemblyName>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Create `Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Runtime\Scry.Core\Scry.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Create `Tests/Scry.Core.Tests/SmokeTests.cs`**

```csharp
using NUnit.Framework;

namespace Scry.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void ProjectScaffolding_Compiles()
        {
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 7: Run `dotnet test` and verify the smoke test passes**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: build succeeds, output ends with `Passed!` and `1` test passed.

- [ ] **Step 8: Create `Editor/Scry.Core.Unity/Scry.Core.Unity.asmdef`**

```json
{
    "name": "Scry.Core.Unity",
    "rootNamespace": "Scry.Core.Unity",
    "references": [
        "Scry.Core"
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

- [ ] **Step 9: Create `Tests/Scry.Core.Unity.Tests/Scry.Core.Unity.Tests.asmdef`**

```json
{
    "name": "Scry.Core.Unity.Tests",
    "rootNamespace": "Scry.Core.Unity.Tests",
    "references": [
        "Scry.Core",
        "Scry.Core.Unity",
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

- [ ] **Step 10: Create the `TestProject` harness**

Create `TestProject/Assets/.gitkeep` (empty file).

Create `TestProject/ProjectSettings/ProjectVersion.txt`:

```
m_EditorVersion: 6000.3.10f1
m_EditorVersionWithRevision: 6000.3.10f1 (e35f0c77bd8e)
```

Create `TestProject/Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.scry.tool": "file:..",
    "com.unity.test-framework": "1.6.0"
  }
}
```

- [ ] **Step 11: Run Unity batch-mode compile against `TestProject`**

Close the Unity editor if it is open (batch mode fails silently otherwise). Run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\compile_check.txt"
```

Expected: exit code 0; `compile_check.txt` ends with `Exiting batchmode successfully now!` and contains no `Scripts have compiler errors` line. Delete `compile_check.txt` after checking (it's a scratch log, not a tracked file).

- [ ] **Step 12: Commit**

```bash
git add package.json .gitignore Runtime Editor Tests TestProject
git commit -m "chore: scaffold Core/Core.Unity assemblies and TestProject compile harness"
```

---

### Task 2: `FieldType` + `FieldDescriptor`

**Files:**
- Create: `Runtime/Scry.Core/FieldType.cs`
- Create: `Runtime/Scry.Core/FieldDescriptor.cs`
- Test: `Tests/Scry.Core.Tests/FieldDescriptorTests.cs`

**Interfaces:**
- Consumes: nothing (first real `Core` types).
- Produces: `enum FieldType { Numeric, String, Boolean, Enum, Reference, Unsupported }` and `class FieldDescriptor { string Name; FieldType Type; bool IsSupported; }`, consumed by `Schema` (Task 3), `SchemaMapper` (Task 7), and every `ValidationRule` (Tasks 5-6).

- [ ] **Step 1: Write the failing tests**

```csharp
// Tests/Scry.Core.Tests/FieldDescriptorTests.cs
using System;
using NUnit.Framework;

namespace Scry.Core.Tests
{
    public class FieldDescriptorTests
    {
        [Test]
        public void Constructor_SetsNameAndType()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);

            Assert.AreEqual("weight", field.Name);
            Assert.AreEqual(FieldType.Numeric, field.Type);
        }

        [Test]
        public void IsSupported_FalseForUnsupportedType()
        {
            var field = new FieldDescriptor("mystery", FieldType.Unsupported);

            Assert.IsFalse(field.IsSupported);
        }

        [Test]
        public void IsSupported_TrueForKnownType()
        {
            var field = new FieldDescriptor("itemName", FieldType.String);

            Assert.IsTrue(field.IsSupported);
        }

        [Test]
        public void Constructor_ThrowsOnEmptyName()
        {
            Assert.Throws<ArgumentException>(() => new FieldDescriptor("", FieldType.String));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: FAIL to compile — `FieldDescriptor` and `FieldType` do not exist.

- [ ] **Step 3: Implement `FieldType`**

```csharp
// Runtime/Scry.Core/FieldType.cs
namespace Scry.Core
{
    public enum FieldType
    {
        Numeric,
        String,
        Boolean,
        Enum,
        Reference,
        Unsupported
    }
}
```

- [ ] **Step 4: Implement `FieldDescriptor`**

```csharp
// Runtime/Scry.Core/FieldDescriptor.cs
using System;

namespace Scry.Core
{
    public sealed class FieldDescriptor
    {
        public string Name { get; }
        public FieldType Type { get; }
        public bool IsSupported => Type != FieldType.Unsupported;

        public FieldDescriptor(string name, FieldType type)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Field name must not be empty.", nameof(name));

            Name = name;
            Type = type;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: PASS, 4 tests passed (plus the Task 1 smoke test, 5 total).

- [ ] **Step 6: Commit**

```bash
git add Runtime/Scry.Core/FieldType.cs Runtime/Scry.Core/FieldDescriptor.cs Tests/Scry.Core.Tests/FieldDescriptorTests.cs
git commit -m "feat(core): add FieldType and FieldDescriptor"
```

---

### Task 3: `Schema`

**Files:**
- Create: `Runtime/Scry.Core/Schema.cs`
- Test: `Tests/Scry.Core.Tests/SchemaTests.cs`

**Interfaces:**
- Consumes: `FieldDescriptor` (Task 2).
- Produces: `class Schema { string TypeName; IReadOnlyList<FieldDescriptor> Fields; FieldDescriptor GetField(string name); }`, consumed by `DataCollection` (Task 4), `SchemaMapper` (Task 7), and `BatchEntryPoint` (Task 10).

- [ ] **Step 1: Write the failing tests**

```csharp
// Tests/Scry.Core.Tests/SchemaTests.cs
using NUnit.Framework;

namespace Scry.Core.Tests
{
    public class SchemaTests
    {
        [Test]
        public void GetField_ReturnsMatchingDescriptor()
        {
            var schema = new Schema("Item", new[]
            {
                new FieldDescriptor("weight", FieldType.Numeric),
                new FieldDescriptor("itemName", FieldType.String)
            });

            var field = schema.GetField("itemName");

            Assert.IsNotNull(field);
            Assert.AreEqual(FieldType.String, field.Type);
        }

        [Test]
        public void GetField_ReturnsNullForUnknownField()
        {
            var schema = new Schema("Item", new[] { new FieldDescriptor("weight", FieldType.Numeric) });

            Assert.IsNull(schema.GetField("doesNotExist"));
        }

        [Test]
        public void Fields_ExposesAllDescriptorsInOrder()
        {
            var schema = new Schema("Item", new[]
            {
                new FieldDescriptor("a", FieldType.Numeric),
                new FieldDescriptor("b", FieldType.String)
            });

            Assert.AreEqual(2, schema.Fields.Count);
            Assert.AreEqual("a", schema.Fields[0].Name);
            Assert.AreEqual("b", schema.Fields[1].Name);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: FAIL to compile — `Schema` does not exist.

- [ ] **Step 3: Implement `Schema`**

```csharp
// Runtime/Scry.Core/Schema.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core
{
    public sealed class Schema
    {
        public string TypeName { get; }
        public IReadOnlyList<FieldDescriptor> Fields { get; }

        public Schema(string typeName, IEnumerable<FieldDescriptor> fields)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Schema type name must not be empty.", nameof(typeName));

            TypeName = typeName;
            Fields = (fields ?? throw new ArgumentNullException(nameof(fields))).ToList();
        }

        public FieldDescriptor GetField(string name)
        {
            return Fields.FirstOrDefault(f => f.Name == name);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: PASS, 3 new tests passed (8 total).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Scry.Core/Schema.cs Tests/Scry.Core.Tests/SchemaTests.cs
git commit -m "feat(core): add Schema"
```

---

### Task 4: `DataRecord` + `DataCollection`

**Files:**
- Create: `Runtime/Scry.Core/DataRecord.cs`
- Create: `Runtime/Scry.Core/DataCollection.cs`
- Test: `Tests/Scry.Core.Tests/DataCollectionTests.cs`

**Interfaces:**
- Consumes: `Schema` (Task 3).
- Produces:
  - `class DataRecord { string Id; string Fingerprint; DataRecord(string id, IReadOnlyDictionary<string,object> values, string fingerprint = null); object GetValue(string fieldName); IReadOnlyDictionary<string,object> Values; }`
  - `class DataCollection { Schema Schema; IReadOnlyList<DataRecord> Records; DataCollection(Schema schema, IEnumerable<DataRecord> records); }`

  `Fingerprint` is an opaque string set by whoever constructs the record (e.g. `Core.Unity`'s asset content hash) — `Core` itself has no concept of what produced it, only that a non-null fingerprint can be compared later to detect external changes (used by `ScriptableObjectRepository.ApplyEdit` in Task 9).

  Consumed by every `ValidationRule` (Tasks 5-6) and `ScriptableObjectRepository` (Tasks 8-9).

- [ ] **Step 1: Write the failing tests**

```csharp
// Tests/Scry.Core.Tests/DataCollectionTests.cs
using System.Collections.Generic;
using NUnit.Framework;

namespace Scry.Core.Tests
{
    public class DataCollectionTests
    {
        [Test]
        public void Records_ExposesValuesByFieldName()
        {
            var schema = new Schema("Item", new[] { new FieldDescriptor("weight", FieldType.Numeric) });
            var record = new DataRecord("asset-1", new Dictionary<string, object> { ["weight"] = 5 });
            var collection = new DataCollection(schema, new[] { record });

            Assert.AreEqual(1, collection.Records.Count);
            Assert.AreEqual(5, collection.Records[0].GetValue("weight"));
        }

        [Test]
        public void GetValue_ReturnsNullForMissingField()
        {
            var record = new DataRecord("asset-1", new Dictionary<string, object>());

            Assert.IsNull(record.GetValue("doesNotExist"));
        }

        [Test]
        public void Fingerprint_DefaultsToNull()
        {
            var record = new DataRecord("asset-1", new Dictionary<string, object>());

            Assert.IsNull(record.Fingerprint);
        }

        [Test]
        public void Fingerprint_CanBeSetExplicitly()
        {
            var record = new DataRecord("asset-1", new Dictionary<string, object>(), fingerprint: "hash-abc");

            Assert.AreEqual("hash-abc", record.Fingerprint);
        }

        [Test]
        public void Constructor_ThrowsOnEmptyId()
        {
            Assert.Throws<System.ArgumentException>(() => new DataRecord("", new Dictionary<string, object>()));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: FAIL to compile — `DataRecord` and `DataCollection` do not exist.

- [ ] **Step 3: Implement `DataRecord`**

```csharp
// Runtime/Scry.Core/DataRecord.cs
using System;
using System.Collections.Generic;

namespace Scry.Core
{
    public sealed class DataRecord
    {
        public string Id { get; }
        public string Fingerprint { get; }
        public IReadOnlyDictionary<string, object> Values => _values;

        private readonly Dictionary<string, object> _values;

        public DataRecord(string id, IReadOnlyDictionary<string, object> values, string fingerprint = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Record id must not be empty.", nameof(id));

            Id = id;
            Fingerprint = fingerprint;
            _values = new Dictionary<string, object>(values ?? throw new ArgumentNullException(nameof(values)));
        }

        public object GetValue(string fieldName)
        {
            return _values.TryGetValue(fieldName, out var value) ? value : null;
        }
    }
}
```

- [ ] **Step 4: Implement `DataCollection`**

```csharp
// Runtime/Scry.Core/DataCollection.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core
{
    public sealed class DataCollection
    {
        public Schema Schema { get; }
        public IReadOnlyList<DataRecord> Records { get; }

        public DataCollection(Schema schema, IEnumerable<DataRecord> records)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Records = (records ?? throw new ArgumentNullException(nameof(records))).ToList();
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: PASS, 5 new tests passed (13 total).

- [ ] **Step 6: Commit**

```bash
git add Runtime/Scry.Core/DataRecord.cs Runtime/Scry.Core/DataCollection.cs Tests/Scry.Core.Tests/DataCollectionTests.cs
git commit -m "feat(core): add DataRecord and DataCollection"
```

---

### Task 5: `ValidationIssue` + `ValidationRule` base + `SumEqualsRule`

**Files:**
- Create: `Runtime/Scry.Core/ValidationSeverity.cs`
- Create: `Runtime/Scry.Core/ValidationIssue.cs`
- Create: `Runtime/Scry.Core/ValidationRule.cs`
- Create: `Runtime/Scry.Core/Rules/SumEqualsRule.cs`
- Test: `Tests/Scry.Core.Tests/Rules/SumEqualsRuleTests.cs`

**Interfaces:**
- Consumes: `DataCollection`, `DataRecord` (Task 4).
- Produces:
  - `enum ValidationSeverity { Error, Warning }`
  - `class ValidationIssue { string RecordId; string FieldName; string Message; ValidationSeverity Severity; }`
  - `abstract class ValidationRule { abstract IEnumerable<ValidationIssue> Evaluate(DataCollection collection); }` — the extension point every concrete rule (this task and Task 6) and later Pillar 1/2 validation wiring builds on.
  - `class SumEqualsRule : ValidationRule` — configured with a numeric field, a target sum, a tolerance, and an optional `groupByField` (e.g. group Adventure Dreams' 45 drop tables by a `"tableId"` field so each table's weights are checked to sum to 100 independently — the grouping key itself is just configuration, nothing here knows about "rarity" or "drop tables").

- [ ] **Step 1: Write the failing tests**

```csharp
// Tests/Scry.Core.Tests/Rules/SumEqualsRuleTests.cs
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Scry.Core.Tests.Rules
{
    public class SumEqualsRuleTests
    {
        private static DataCollection BuildCollection(params (string table, double weight)[] rows)
        {
            var schema = new Schema("DropEntry", new[]
            {
                new FieldDescriptor("table", FieldType.String),
                new FieldDescriptor("weight", FieldType.Numeric)
            });

            var records = rows.Select((row, i) => new DataRecord($"row-{i}", new Dictionary<string, object>
            {
                ["table"] = row.table,
                ["weight"] = row.weight
            }));

            return new DataCollection(schema, records);
        }

        [Test]
        public void Evaluate_NoIssue_WhenGroupSumsMatchTarget()
        {
            var collection = BuildCollection(("goblin", 60), ("goblin", 40), ("wolf", 100));
            var rule = new SumEqualsRule("weight", target: 100, groupByField: "table");

            var issues = rule.Evaluate(collection).ToList();

            Assert.IsEmpty(issues);
        }

        [Test]
        public void Evaluate_ReportsIssue_WhenGroupSumDoesNotMatchTarget()
        {
            var collection = BuildCollection(("goblin", 60), ("goblin", 30));
            var rule = new SumEqualsRule("weight", target: 100, groupByField: "table");

            var issues = rule.Evaluate(collection).ToList();

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("goblin", issues[0].RecordId);
            Assert.AreEqual(ValidationSeverity.Error, issues[0].Severity);
        }

        [Test]
        public void Evaluate_UngroupedSumsAcrossWholeCollection_WhenGroupByFieldOmitted()
        {
            var collection = BuildCollection(("goblin", 60), ("wolf", 40));
            var rule = new SumEqualsRule("weight", target: 100);

            var issues = rule.Evaluate(collection).ToList();

            Assert.IsEmpty(issues);
        }

        [Test]
        public void Evaluate_RespectsTolerance()
        {
            var collection = BuildCollection(("goblin", 99.995));
            var rule = new SumEqualsRule("weight", target: 100, tolerance: 0.01);

            var issues = rule.Evaluate(collection).ToList();

            Assert.IsEmpty(issues);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: FAIL to compile — `SumEqualsRule` and its dependencies do not exist.

- [ ] **Step 3: Implement `ValidationSeverity`**

```csharp
// Runtime/Scry.Core/ValidationSeverity.cs
namespace Scry.Core
{
    public enum ValidationSeverity
    {
        Error,
        Warning
    }
}
```

- [ ] **Step 4: Implement `ValidationIssue`**

```csharp
// Runtime/Scry.Core/ValidationIssue.cs
namespace Scry.Core
{
    public sealed class ValidationIssue
    {
        public string RecordId { get; }
        public string FieldName { get; }
        public string Message { get; }
        public ValidationSeverity Severity { get; }

        public ValidationIssue(string recordId, string fieldName, string message, ValidationSeverity severity = ValidationSeverity.Error)
        {
            RecordId = recordId;
            FieldName = fieldName;
            Message = message;
            Severity = severity;
        }
    }
}
```

- [ ] **Step 5: Implement `ValidationRule` base**

```csharp
// Runtime/Scry.Core/ValidationRule.cs
using System.Collections.Generic;

namespace Scry.Core
{
    public abstract class ValidationRule
    {
        public abstract IEnumerable<ValidationIssue> Evaluate(DataCollection collection);
    }
}
```

- [ ] **Step 6: Implement `SumEqualsRule`**

```csharp
// Runtime/Scry.Core/Rules/SumEqualsRule.cs
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

        public SumEqualsRule(string field, double target, double tolerance = 0.0001, string groupByField = null)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _target = target;
            _tolerance = tolerance;
            _groupByField = groupByField;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            var groups = _groupByField == null
                ? new[] { (Key: UngroupedKey, Records: (IEnumerable<DataRecord>)collection.Records) }
                : collection.Records
                    .GroupBy(r => Convert.ToString(r.GetValue(_groupByField)))
                    .Select(g => (Key: g.Key, Records: (IEnumerable<DataRecord>)g))
                    .ToArray();

            foreach (var group in groups)
            {
                var sum = group.Records.Sum(r => Convert.ToDouble(r.GetValue(_field) ?? 0));

                if (Math.Abs(sum - _target) > _tolerance)
                {
                    yield return new ValidationIssue(
                        recordId: group.Key,
                        fieldName: _field,
                        message: $"Sum of '{_field}' in group '{group.Key}' is {sum}, expected {_target}.");
                }
            }
        }
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: PASS, 4 new tests passed (17 total).

- [ ] **Step 8: Commit**

```bash
git add Runtime/Scry.Core/ValidationSeverity.cs Runtime/Scry.Core/ValidationIssue.cs Runtime/Scry.Core/ValidationRule.cs Runtime/Scry.Core/Rules/SumEqualsRule.cs Tests/Scry.Core.Tests/Rules/SumEqualsRuleTests.cs
git commit -m "feat(core): add ValidationRule base and SumEqualsRule"
```

---

### Task 6: `NoDuplicateRule` + `RequiredAtLeastOnceRule`

**Files:**
- Create: `Runtime/Scry.Core/Rules/NoDuplicateRule.cs`
- Create: `Runtime/Scry.Core/Rules/RequiredAtLeastOnceRule.cs`
- Test: `Tests/Scry.Core.Tests/Rules/NoDuplicateRuleTests.cs`
- Test: `Tests/Scry.Core.Tests/Rules/RequiredAtLeastOnceRuleTests.cs`

**Interfaces:**
- Consumes: `ValidationRule`, `DataCollection`, `DataRecord`, `ValidationIssue` (Task 5).
- Produces: `class NoDuplicateRule : ValidationRule` (configured with a field name; flags records whose value for that field repeats) and `class RequiredAtLeastOnceRule : ValidationRule` (configured with a field name + required value; flags when no record satisfies it — e.g. "at least one item must have `isStarterWeapon == true`", expressed purely as configuration).

- [ ] **Step 1: Write the failing tests**

```csharp
// Tests/Scry.Core.Tests/Rules/NoDuplicateRuleTests.cs
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Scry.Core.Tests.Rules
{
    public class NoDuplicateRuleTests
    {
        private static DataCollection BuildCollection(params (string id, string itemId)[] rows)
        {
            var schema = new Schema("Item", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var records = rows.Select(r => new DataRecord(r.id, new Dictionary<string, object> { ["itemId"] = r.itemId }));
            return new DataCollection(schema, records);
        }

        [Test]
        public void Evaluate_NoIssue_WhenAllValuesUnique()
        {
            var collection = BuildCollection(("r1", "sword"), ("r2", "shield"));
            var rule = new NoDuplicateRule("itemId");

            Assert.IsEmpty(rule.Evaluate(collection));
        }

        [Test]
        public void Evaluate_ReportsIssue_OnDuplicateValue()
        {
            var collection = BuildCollection(("r1", "sword"), ("r2", "sword"));
            var rule = new NoDuplicateRule("itemId");

            var issues = rule.Evaluate(collection).ToList();

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("r2", issues[0].RecordId);
        }

        [Test]
        public void Evaluate_IgnoresNullValues()
        {
            var schema = new Schema("Item", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var records = new[]
            {
                new DataRecord("r1", new Dictionary<string, object> { ["itemId"] = null }),
                new DataRecord("r2", new Dictionary<string, object> { ["itemId"] = null })
            };
            var collection = new DataCollection(schema, records);
            var rule = new NoDuplicateRule("itemId");

            Assert.IsEmpty(rule.Evaluate(collection));
        }
    }
}
```

```csharp
// Tests/Scry.Core.Tests/Rules/RequiredAtLeastOnceRuleTests.cs
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Scry.Core.Tests.Rules
{
    public class RequiredAtLeastOnceRuleTests
    {
        private static DataCollection BuildCollection(params bool[] isStarterValues)
        {
            var schema = new Schema("Item", new[] { new FieldDescriptor("isStarter", FieldType.Boolean) });
            var records = isStarterValues.Select((v, i) => new DataRecord($"r{i}", new Dictionary<string, object> { ["isStarter"] = v }));
            return new DataCollection(schema, records);
        }

        [Test]
        public void Evaluate_NoIssue_WhenAtLeastOneRecordMatches()
        {
            var collection = BuildCollection(false, true, false);
            var rule = new RequiredAtLeastOnceRule("isStarter", true);

            Assert.IsEmpty(rule.Evaluate(collection));
        }

        [Test]
        public void Evaluate_ReportsIssue_WhenNoRecordMatches()
        {
            var collection = BuildCollection(false, false);
            var rule = new RequiredAtLeastOnceRule("isStarter", true);

            var issues = rule.Evaluate(collection).ToList();

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("isStarter", issues[0].FieldName);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: FAIL to compile — `NoDuplicateRule` and `RequiredAtLeastOnceRule` do not exist.

- [ ] **Step 3: Implement `NoDuplicateRule`**

```csharp
// Runtime/Scry.Core/Rules/NoDuplicateRule.cs
using System;
using System.Collections.Generic;

namespace Scry.Core.Rules
{
    public sealed class NoDuplicateRule : ValidationRule
    {
        private readonly string _field;

        public NoDuplicateRule(string field)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            var seen = new Dictionary<object, string>();

            foreach (var record in collection.Records)
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

- [ ] **Step 4: Implement `RequiredAtLeastOnceRule`**

```csharp
// Runtime/Scry.Core/Rules/RequiredAtLeastOnceRule.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core.Rules
{
    public sealed class RequiredAtLeastOnceRule : ValidationRule
    {
        private readonly string _field;
        private readonly object _requiredValue;

        public RequiredAtLeastOnceRule(string field, object requiredValue)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _requiredValue = requiredValue;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            var found = collection.Records.Any(r => Equals(r.GetValue(_field), _requiredValue));

            if (!found)
            {
                yield return new ValidationIssue(
                    recordId: null,
                    fieldName: _field,
                    message: $"No record has '{_field}' equal to '{_requiredValue}'; at least one is required.");
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Tests/Scry.Core.Tests/Scry.Core.Tests.csproj`
Expected: PASS, 5 new tests passed (22 total).

- [ ] **Step 6: Commit**

```bash
git add Runtime/Scry.Core/Rules/NoDuplicateRule.cs Runtime/Scry.Core/Rules/RequiredAtLeastOnceRule.cs Tests/Scry.Core.Tests/Rules/NoDuplicateRuleTests.cs Tests/Scry.Core.Tests/Rules/RequiredAtLeastOnceRuleTests.cs
git commit -m "feat(core): add NoDuplicateRule and RequiredAtLeastOnceRule"
```

`Core` is now feature-complete for this plan: `Schema`/`FieldDescriptor`, `DataCollection`/`DataRecord`, and the three `ValidationRule` primitives from the spec, all dotnet-testable with zero Unity dependency. Tasks 7-10 build `Core.Unity` on top.

---

## Mid-plan correction: package moved into `Package/`

When Task 7 first attempted to run Unity EditMode tests, it hit a structural bug: `TestProject/Packages/manifest.json` resolved `com.scry.tool` to the repo root via `file:../..`, which recursively included `TestProject/` (and its own `Library/` build cache) as package content. Unity's asset scanner raced against Bee's transient build artifacts under `TestProject/Library/Bee/artifacts/...`, causing non-converging rebuilds and `CS0006` errors — never a clean compile, across 4 attempts and ~25 minutes of Unity wall-clock time. As corroborating evidence, Unity was also generating stray `.meta` files for things with no business being package content (`CLAUDE.md.meta`, `docs.meta`, `TestProject.meta` itself) — proof the package root was effectively "everything in the repo."

The human chose to move the actual package (`package.json`, `Runtime/`, `Editor/`, `Tests/`) into a new `Package/` subfolder, so the package no longer recursively contains `TestProject/` (which stays a sibling at the repo root, unchanged). `TestProject/Packages/manifest.json`'s `"com.scry.tool"` now resolves via `"file:../../Package"` instead of `"file:../.."`. This was executed as a corrective infrastructure commit before Task 7 resumed. It also caught and fixed a related gap: Tasks 2-6 had never committed the `.meta` sidecar files Unity generates for their `.cs`/`.asmdef` files (only Task 1's own scaffolding `.meta` files had been tracked) — those are now tracked too, for stable GUIDs.

**All file paths below, from Task 7 onward, already reflect the corrected `Package/`-prefixed layout** (e.g. `Package/Runtime/Scry.Core/...`, `Package/Editor/Scry.Core.Unity/...`, `Package/Tests/Scry.Core.Tests/...`). Tasks 1-6 above are historical and describe paths as they were *before* this correction — those tasks already executed and are not being retroactively rewritten. `TestProject/` paths are unaffected throughout (it never moved).

---

### Task 7: `SchemaMapper`

**Files:**
- Create: `Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestItemData.cs`
- Create: `Package/Editor/Scry.Core.Unity/SchemaMapper.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/SchemaMapperTests.cs`

**Interfaces:**
- Consumes: `Schema`, `FieldDescriptor`, `FieldType` (Tasks 2-3).
- Produces: `static class SchemaMapper { static Schema InferSchema(Type scriptableObjectType); }`, consumed by `ScriptableObjectRepository` (Task 8) and `BatchEntryPoint` (Task 10).

This is the first `Core.Unity` code and the first test that needs the Unity Editor (EditMode tests run inside Unity, not via `dotnet test`).

- [ ] **Step 1: Create the fixture `ScriptableObject` type**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestItemData.cs
using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public enum TestRarity
    {
        Common,
        Rare
    }

    public class TestItemData : ScriptableObject
    {
        public string itemName;
        public int weight;
        [SerializeField] private float dropChance;
        public bool isUnique;
        public TestRarity rarity;
        public TestItemData referencedItem;
        public Vector3 unsupportedField;

        public float DropChance => dropChance;
    }
}
```

`dropChance` is a private `[SerializeField]` (tests that `SchemaMapper` reads non-public serialized fields, not just public ones). `unsupportedField` is a `Vector3`, a type `SchemaMapper` doesn't recognize — it exercises the "mark unsupported, don't throw" path required by the spec's error-handling section.

- [ ] **Step 2: Write the failing test**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/SchemaMapperTests.cs
using NUnit.Framework;
using Scry.Core;
using Scry.Core.Unity.Tests.Fixtures;

namespace Scry.Core.Unity.Tests
{
    public class SchemaMapperTests
    {
        [Test]
        public void InferSchema_MapsKnownFieldTypes()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestItemData));

            Assert.AreEqual(FieldType.String, schema.GetField("itemName").Type);
            Assert.AreEqual(FieldType.Numeric, schema.GetField("weight").Type);
            Assert.AreEqual(FieldType.Numeric, schema.GetField("dropChance").Type);
            Assert.AreEqual(FieldType.Boolean, schema.GetField("isUnique").Type);
            Assert.AreEqual(FieldType.Enum, schema.GetField("rarity").Type);
            Assert.AreEqual(FieldType.Reference, schema.GetField("referencedItem").Type);
        }

        [Test]
        public void InferSchema_MarksUnmappableFieldAsUnsupported_WithoutThrowing()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestItemData));

            var field = schema.GetField("unsupportedField");

            Assert.IsNotNull(field);
            Assert.AreEqual(FieldType.Unsupported, field.Type);
        }

        [Test]
        public void InferSchema_UsesTypeNameAsSchemaTypeName()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestItemData));

            Assert.AreEqual("TestItemData", schema.TypeName);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Close the Unity editor if open, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -testPlatform EditMode -testResults "C:\Users\gugal\Documents\projetos\scry\TestProject\test_results.xml" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\test_run.txt"
```

Expected: compile failure — `SchemaMapper` does not exist yet. Check `test_run.txt` for `Scripts have compiler errors`.

- [ ] **Step 4: Implement `SchemaMapper`**

```csharp
// Package/Editor/Scry.Core.Unity/SchemaMapper.cs
using System;
using System.Collections.Generic;
using System.Linq;
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

            var members = scriptableObjectType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null);

            var fields = members
                .Select(m => new FieldDescriptor(m.Name, MapFieldType(m.FieldType)))
                .ToList();

            return new Schema(scriptableObjectType.Name, fields);
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

Numeric support is intentionally scoped to `int`/`float` for this plan — `long`/`double` fields fall through to `Unsupported` rather than being read incorrectly, consistent with "degrade gracefully rather than guess." Widening numeric support is a follow-up, not a gap hidden behind wrong behavior.

- [ ] **Step 5: Run tests to verify they pass**

Re-run the same batch-mode test command from Step 3.
Expected: exit code 0; `test_results.xml` shows 3 passed, 0 failed for `SchemaMapperTests`. Delete `test_results.xml` and `test_run.txt` after checking (scratch logs).

- [ ] **Step 6: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/SchemaMapper.cs Package/Tests/Scry.Core.Unity.Tests/Fixtures/TestItemData.cs Package/Tests/Scry.Core.Unity.Tests/SchemaMapperTests.cs
git commit -m "feat(core.unity): add SchemaMapper"
```

---

### Task 8: `ScriptableObjectRepository.Scan` (read path)

**Files:**
- Create: `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs`

**Interfaces:**
- Consumes: `SchemaMapper.InferSchema` (Task 7), `DataCollection`, `DataRecord` (Task 4).
- Produces: `class ScriptableObjectRepository { DataCollection Scan(Type scriptableObjectType); }`. `Scan` is the one read path shared by editing, simulation, and CI per the spec's data-flow section — every later consumer (Pillar 1's `DataGridWindow`, Pillar 2's simulation setup, `BatchEntryPoint` in Task 10) calls this same method, never a second copy of the read logic.

- [ ] **Step 1: Write the failing test**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Scry.Core.Unity.Tests.Fixtures;

namespace Scry.Core.Unity.Tests
{
    public class ScriptableObjectRepositoryTests
    {
        private const string FixtureFolder = "Assets/ScryTestFixtures";
        private ScriptableObjectRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _repository = new ScriptableObjectRepository();
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
                AssetDatabase.CreateFolder("Assets", "ScryTestFixtures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }

        [Test]
        public void Scan_ReadsFieldValuesFromRealAssets()
        {
            var item = ScriptableObject.CreateInstance<TestItemData>();
            item.itemName = "Rusty Sword";
            item.weight = 5;
            item.isUnique = true;
            AssetDatabase.CreateAsset(item, $"{FixtureFolder}/RustySword.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestItemData));

            Assert.AreEqual(1, collection.Records.Count);
            var record = collection.Records[0];
            Assert.AreEqual("Rusty Sword", record.GetValue("itemName"));
            Assert.AreEqual(5, record.GetValue("weight"));
            Assert.AreEqual(true, record.GetValue("isUnique"));
        }

        [Test]
        public void Scan_SetsNonNullFingerprintPerRecord()
        {
            var item = ScriptableObject.CreateInstance<TestItemData>();
            AssetDatabase.CreateAsset(item, $"{FixtureFolder}/Fingerprinted.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestItemData));

            Assert.IsNotNull(collection.Records[0].Fingerprint);
        }

        [Test]
        public void Scan_ReturnsEmptyCollection_WhenNoAssetsExist()
        {
            var collection = _repository.Scan(typeof(TestItemData));

            Assert.IsEmpty(collection.Records);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the same batch-mode EditMode test command as Task 7 Step 3.
Expected: compile failure — `ScriptableObjectRepository` does not exist.

- [ ] **Step 3: Implement `ScriptableObjectRepository.Scan`**

```csharp
// Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs
using System;
using System.Collections.Generic;
using Scry.Core;
using UnityEditor;
using UnityEngine;

namespace Scry.Core.Unity
{
    public sealed class ScriptableObjectRepository
    {
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
                    values[field.Name] = ReadValue(property, field.Type);
                }

                var fingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
                records.Add(new DataRecord(guid, values, fingerprint));
            }

            return new DataCollection(schema, records);
        }

        internal static object ReadValue(SerializedProperty property, FieldType type)
        {
            if (property == null)
                return null;

            switch (type)
            {
                case FieldType.Numeric:
                    return property.propertyType == SerializedPropertyType.Integer
                        ? (object)property.intValue
                        : property.floatValue;
                case FieldType.String:
                    return property.stringValue;
                case FieldType.Boolean:
                    return property.boolValue;
                case FieldType.Enum:
                    return property.enumValueIndex;
                case FieldType.Reference:
                    return property.objectReferenceValue;
                default:
                    return null;
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch-mode EditMode test command.
Expected: exit code 0; 3 new tests passed for `ScriptableObjectRepositoryTests`.

- [ ] **Step 5: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs
git commit -m "feat(core.unity): add ScriptableObjectRepository read path"
```

---

### Task 9: `ScriptableObjectRepository.ApplyEdit` + write-conflict detection

**Files:**
- Create: `Package/Editor/Scry.Core.Unity/WriteConflictException.cs`
- Modify: `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`
- Modify: `Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs`

**Interfaces:**
- Consumes: `ScriptableObjectRepository.Scan`, `DataRecord.Fingerprint` (Tasks 4, 8).
- Produces: `class WriteConflictException : Exception { string RecordId; }` and `ScriptableObjectRepository.ApplyEdit(DataRecord record, string fieldName, object value, Type scriptableObjectType)`. Throws `WriteConflictException` if the asset's current content hash no longer matches `record.Fingerprint` — implements the spec's "detected before applying and surfaced as a reload prompt, not silently overwritten" requirement. Consumed later by Pillar 1's `DataGridWindow` on cell edit.

- [ ] **Step 1: Write the failing tests**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs — add inside the existing class
[Test]
public void ApplyEdit_WritesValueBackToAsset()
{
    var item = ScriptableObject.CreateInstance<TestItemData>();
    item.itemName = "Rusty Sword";
    var path = $"{FixtureFolder}/RustySword.asset";
    AssetDatabase.CreateAsset(item, path);
    AssetDatabase.SaveAssets();

    var collection = _repository.Scan(typeof(TestItemData));
    var record = collection.Records[0];

    _repository.ApplyEdit(record, "itemName", "Legendary Sword", typeof(TestItemData));

    var reloaded = _repository.Scan(typeof(TestItemData));
    Assert.AreEqual("Legendary Sword", reloaded.Records[0].GetValue("itemName"));
}

[Test]
public void ApplyEdit_ThrowsWriteConflict_WhenAssetChangedExternallySinceScan()
{
    var item = ScriptableObject.CreateInstance<TestItemData>();
    item.itemName = "Rusty Sword";
    var path = $"{FixtureFolder}/RustySword.asset";
    AssetDatabase.CreateAsset(item, path);
    AssetDatabase.SaveAssets();

    var collection = _repository.Scan(typeof(TestItemData));
    var record = collection.Records[0];

    var externallyLoaded = AssetDatabase.LoadAssetAtPath<TestItemData>(path);
    externallyLoaded.itemName = "Changed By Someone Else";
    EditorUtility.SetDirty(externallyLoaded);
    AssetDatabase.SaveAssets();

    Assert.Throws<WriteConflictException>(() =>
        _repository.ApplyEdit(record, "itemName", "My Edit", typeof(TestItemData)));
}
```

Add `using UnityEditor;` if not already present (it is, from Task 8).

- [ ] **Step 2: Run tests to verify they fail**

Run the batch-mode EditMode test command from Task 7.
Expected: compile failure — `ApplyEdit` and `WriteConflictException` do not exist.

- [ ] **Step 3: Implement `WriteConflictException`**

```csharp
// Package/Editor/Scry.Core.Unity/WriteConflictException.cs
using System;

namespace Scry.Core.Unity
{
    public sealed class WriteConflictException : Exception
    {
        public string RecordId { get; }

        public WriteConflictException(string recordId)
            : base($"Asset for record '{recordId}' changed since it was loaded. Reload before saving.")
        {
            RecordId = recordId;
        }
    }
}
```

- [ ] **Step 4: Implement `ApplyEdit` on `ScriptableObjectRepository`**

Add to `Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs`, inside the `ScriptableObjectRepository` class:

```csharp
public void ApplyEdit(DataRecord record, string fieldName, object value, Type scriptableObjectType)
{
    var path = AssetDatabase.GUIDToAssetPath(record.Id);
    var currentFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();

    if (record.Fingerprint != null && currentFingerprint != record.Fingerprint)
        throw new WriteConflictException(record.Id);

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
}

private static void WriteValue(SerializedProperty property, object value)
{
    switch (property.propertyType)
    {
        case SerializedPropertyType.Integer:
            property.intValue = Convert.ToInt32(value);
            break;
        case SerializedPropertyType.Float:
            property.floatValue = Convert.ToSingle(value);
            break;
        case SerializedPropertyType.String:
            property.stringValue = (string)value;
            break;
        case SerializedPropertyType.Boolean:
            property.boolValue = Convert.ToBoolean(value);
            break;
        case SerializedPropertyType.Enum:
            property.enumValueIndex = Convert.ToInt32(value);
            break;
        case SerializedPropertyType.ObjectReference:
            property.objectReferenceValue = (UnityEngine.Object)value;
            break;
        default:
            throw new NotSupportedException($"Unsupported property type '{property.propertyType}'.");
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Re-run the batch-mode EditMode test command.
Expected: exit code 0; 2 new tests passed for `ScriptableObjectRepositoryTests` (8 total in that fixture).

- [ ] **Step 6: Commit**

```bash
git add Package/Editor/Scry.Core.Unity/WriteConflictException.cs Package/Editor/Scry.Core.Unity/ScriptableObjectRepository.cs Package/Tests/Scry.Core.Unity.Tests/ScriptableObjectRepositoryTests.cs
git commit -m "feat(core.unity): add write-back with conflict detection"
```

---

### Task 10: Batch-mode entry point

**Files:**
- Create: `Package/Editor/Scry.Core.Unity/BatchEntryPoint.cs`
- Test: `Package/Tests/Scry.Core.Unity.Tests/BatchEntryPointTests.cs`
- Create: `TestProject/Assets/ScryManualCheck/SampleItem.cs`
- Create: `TestProject/Assets/ScryManualCheck/SampleSword.asset` (created via the Editor, see Step 6)

**Interfaces:**
- Consumes: `ScriptableObjectRepository.Scan` (Task 8), `Schema.Fields`, `FieldDescriptor.IsSupported` (Tasks 2-3).
- Produces: `static class BatchEntryPoint { const int ExitCodeSuccess = 0; const int ExitCodeValidationFailure = 1; const int ExitCodeToolError = 2; static void ValidateCollection(); static int Run(string[] args); static string GetArgValue(string[] args, string name); }`. `ValidateCollection` is the `-executeMethod` target for CI; `Run`/`GetArgValue` are `public` (not the CLI entry point itself) so they're unit-testable with synthetic argument arrays.

This entry point runs `Scan` — the same read path `DataGridWindow` (future Pillar 1) and simulation setup (future Pillar 2) will use — satisfying "there is exactly one path that reads real project data" from the spec's data flow section. It reports mapping results (unsupported fields as warnings) but does not yet run `ValidationRule`s: rule *configuration* (which rules apply to which type) is authored through Pillar 1's UI, which doesn't exist yet. Wiring configured rules into this entry point — and using `ExitCodeValidationFailure` — is a Pillar 1 follow-up task, not silently faked here.

- [ ] **Step 1: Write the failing tests**

```csharp
// Package/Tests/Scry.Core.Unity.Tests/BatchEntryPointTests.cs
using NUnit.Framework;

namespace Scry.Core.Unity.Tests
{
    public class BatchEntryPointTests
    {
        [Test]
        public void GetArgValue_ReturnsValueFollowingFlag()
        {
            var args = new[] { "Unity.exe", "-batchmode", "-scryType", "My.Namespace.MyType, MyAssembly" };

            var value = BatchEntryPoint.GetArgValue(args, "-scryType");

            Assert.AreEqual("My.Namespace.MyType, MyAssembly", value);
        }

        [Test]
        public void GetArgValue_ReturnsNull_WhenFlagMissing()
        {
            var args = new[] { "Unity.exe", "-batchmode" };

            Assert.IsNull(BatchEntryPoint.GetArgValue(args, "-scryType"));
        }

        [Test]
        public void GetArgValue_ReturnsNull_WhenFlagIsLastArgument()
        {
            var args = new[] { "Unity.exe", "-scryType" };

            Assert.IsNull(BatchEntryPoint.GetArgValue(args, "-scryType"));
        }

        [Test]
        public void Run_ReturnsToolError_WhenTypeArgMissing()
        {
            var args = new[] { "Unity.exe", "-batchmode" };

            Assert.AreEqual(BatchEntryPoint.ExitCodeToolError, BatchEntryPoint.Run(args));
        }

        [Test]
        public void Run_ReturnsToolError_WhenTypeCannotBeResolved()
        {
            var args = new[] { "-scryType", "Does.Not.Exist, Nowhere" };

            Assert.AreEqual(BatchEntryPoint.ExitCodeToolError, BatchEntryPoint.Run(args));
        }

        [Test]
        public void Run_ReturnsSuccess_WhenTypeResolvesAndScanSucceeds()
        {
            var args = new[] { "-scryType", typeof(Fixtures.TestItemData).AssemblyQualifiedName };

            Assert.AreEqual(BatchEntryPoint.ExitCodeSuccess, BatchEntryPoint.Run(args));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the batch-mode EditMode test command from Task 7.
Expected: compile failure — `BatchEntryPoint` does not exist.

- [ ] **Step 3: Implement `BatchEntryPoint`**

```csharp
// Package/Editor/Scry.Core.Unity/BatchEntryPoint.cs
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scry.Core.Unity
{
    public static class BatchEntryPoint
    {
        public const int ExitCodeSuccess = 0;
        public const int ExitCodeValidationFailure = 1;
        public const int ExitCodeToolError = 2;

        public static void ValidateCollection()
        {
            var exitCode = Run(Environment.GetCommandLineArgs());
            EditorApplication.Exit(exitCode);
        }

        public static int Run(string[] args)
        {
            try
            {
                var typeName = GetArgValue(args, "-scryType");
                if (typeName == null)
                {
                    Debug.LogError("Scry: missing required -scryType <AssemblyQualifiedName> argument.");
                    return ExitCodeToolError;
                }

                var type = Type.GetType(typeName);
                if (type == null)
                {
                    Debug.LogError($"Scry: could not resolve type '{typeName}'.");
                    return ExitCodeToolError;
                }

                var repository = new ScriptableObjectRepository();
                var collection = repository.Scan(type);

                foreach (var field in collection.Schema.Fields.Where(f => !f.IsSupported))
                    Debug.LogWarning($"Scry: field '{field.Name}' on '{typeName}' has an unmapped type and was skipped.");

                Debug.Log($"Scry: scanned {collection.Records.Count} record(s) of type '{typeName}'.");
                return ExitCodeSuccess;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Scry: tool error - {ex}");
                return ExitCodeToolError;
            }
        }

        public static string GetArgValue(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
                return null;

            return args[index + 1];
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the batch-mode EditMode test command.
Expected: exit code 0; 6 new tests passed for `BatchEntryPointTests`.

- [ ] **Step 5: Commit the entry point and its tests**

```bash
git add Package/Editor/Scry.Core.Unity/BatchEntryPoint.cs Package/Tests/Scry.Core.Unity.Tests/BatchEntryPointTests.cs
git commit -m "feat(core.unity): add headless batch-mode entry point"
```

- [ ] **Step 6: Create a manual smoke fixture in `TestProject`**

This exercises the real `-executeMethod` CLI path end-to-end, independent of the Tests assembly, matching how a real CI consumer would call it.

```csharp
// TestProject/Assets/ScryManualCheck/SampleItem.cs
using UnityEngine;

namespace ScryManualCheck
{
    public class SampleItem : ScriptableObject
    {
        public string itemName;
        public int weight;
    }
}
```

Open the Unity Editor on `TestProject`, right-click `Assets/ScryManualCheck` → Create → this needs a `CreateAssetMenu` to be easy from the Editor UI, but for a one-off manual fixture it's simpler to create it via a temporary Editor script. Add, run once via the Editor's C# console equivalent (Tools menu), then delete:

```csharp
// Temporary — not committed. Paste into a scratch .cs file under Assets/Editor, invoke via menu, then delete the script.
using UnityEditor;
using UnityEngine;
using ScryManualCheck;

public static class CreateSampleItemAsset
{
    [MenuItem("Tools/Scry/Create Sample Item Asset (one-off)")]
    public static void Create()
    {
        var item = ScriptableObject.CreateInstance<SampleItem>();
        item.itemName = "Sample Sword";
        item.weight = 3;
        AssetDatabase.CreateAsset(item, "Assets/ScryManualCheck/SampleSword.asset");
        AssetDatabase.SaveAssets();
    }
}
```

Run `Tools/Scry/Create Sample Item Asset (one-off)` from the Editor menu, confirm `TestProject/Assets/ScryManualCheck/SampleSword.asset` exists, then delete the temporary menu script (it was scaffolding for this one step, not part of the fixture).

- [ ] **Step 7: Run the batch-mode entry point via the real CLI path**

Close the Editor, then run:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\gugal\Documents\projetos\scry\TestProject" -executeMethod Scry.Core.Unity.BatchEntryPoint.ValidateCollection -scryType "ScryManualCheck.SampleItem, Assembly-CSharp" -logFile "C:\Users\gugal\Documents\projetos\scry\TestProject\manual_check.txt"
```

Expected: exit code 0; `manual_check.txt` contains `Scry: scanned 1 record(s) of type 'ScryManualCheck.SampleItem, Assembly-CSharp'.` Delete `manual_check.txt` after checking.

- [ ] **Step 8: Commit the manual smoke fixture**

```bash
git add "TestProject/Assets/ScryManualCheck/SampleItem.cs" "TestProject/Assets/ScryManualCheck/SampleSword.asset" "TestProject/Assets/ScryManualCheck/SampleSword.asset.meta" "TestProject/Assets/ScryManualCheck/SampleItem.cs.meta" "TestProject/Assets/ScryManualCheck.meta"
git commit -m "test(core.unity): add manual CLI smoke fixture for BatchEntryPoint"
```

---

### Task 11: Document the development workflow in `CLAUDE.md`

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: nothing (documentation only).
- Produces: nothing consumed by later code — this closes out the "Development workflow" section CLAUDE.md currently marks as "Not yet established."

- [ ] **Step 1: Replace the "Development workflow" section**

In `CLAUDE.md`, replace:

```markdown
## Development workflow

Not yet established — no code exists yet. This section should be filled in (build/test commands, compilation verification workflow, etc.) once implementation begins; Adventure Dreams' own `CLAUDE.md` has a good model to follow for the batch-mode compilation check pattern once there's a Unity project here to check.
```

with:

```markdown
## Development workflow

`Core` is plain .NET and tested independently of Unity:

```bash
dotnet test Package/Tests/Scry.Core.Tests/Scry.Core.Tests.csproj
```

`Core.Unity` requires the Unity Editor. `TestProject/` (checked into this repo) is a throwaway harness that references the package via a `file:` dependency — it exists only to compile and test the package, it is not a product of this tool. Close the Editor before running batch mode (it fails silently if the Editor already has `TestProject` open).

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
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: document the dotnet test + Unity batch-mode dev workflow"
```

---

## Self-Review

**Spec coverage:**
- `Schema`/`FieldDescriptor` (spec line 62) → Tasks 2-3.
- `DataCollection` (spec line 63) → Task 4.
- `ValidationRule` with `SumEquals`, `NoDuplicate`, `RequiredAtLeastOnce` primitives (spec line 64) → Tasks 5-6.
- `ScriptableObjectRepository` scan/map/write-back (spec line 72) → Tasks 8-9.
- `SchemaMapper` inferring `Schema` from `[SerializeField]`, unmappable fields marked unsupported (spec line 73) → Task 7.
- Batch-mode entry point via `-executeMethod` (spec line 74) → Task 10.
- Mapping-failure graceful degradation (spec line 98) → Task 7 (`unsupportedField` test) and Task 10 (warning log, doesn't abort).
- Write-back conflict detection (spec line 99) → Task 9.
- Batch-mode/CI exit codes distinguishing validation failures from tool errors (spec line 101) → Task 10 (`ExitCodeValidationFailure` reserved and documented as a Pillar 1 follow-up, since rule configuration doesn't exist yet — `ExitCodeToolError` fully implemented and tested).
- `Core` testable via plain `dotnet test`, `Core.Unity` via Unity Test Framework EditMode/batch mode (spec lines 105-106) → Task 1 scaffolding, exercised throughout.
- `WeightedTable`/`SimulationEngine` (spec lines 65-66) and `UI` assembly (spec lines 76-81) are explicitly out of scope for this plan — they belong to the Pillar 2 and Pillar 1 plans respectively, per the earlier scoping decision.

**Placeholder scan:** No `TODO`/`TBD`/"handle appropriately" language in any task's code. The one deliberately narrowed scope (numeric mapping limited to `int`/`float`, `ExitCodeValidationFailure` unwired) is called out explicitly with the reason, not hidden.

**Type consistency:** `DataRecord(string id, IReadOnlyDictionary<string,object> values, string fingerprint = null)` (Task 4) matches every call site in Tasks 5, 6, 8, 9. `ValidationRule.Evaluate(DataCollection) : IEnumerable<ValidationIssue>` (Task 5) matches the override signature in `SumEqualsRule`, `NoDuplicateRule`, `RequiredAtLeastOnceRule`. `ScriptableObjectRepository.Scan(Type) : DataCollection` (Task 8) and `.ApplyEdit(DataRecord, string, object, Type) : void` (Task 9) match `BatchEntryPoint`'s usage in Task 10.

---

Plan complete and saved to `docs/superpowers/plans/2026-07-17-foundation-core-and-core-unity.md`. Two execution options:

1. **Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
