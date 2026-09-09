using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scry.Core.Rules;

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

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Evaluate_ReportsIssue_WhenGroupSumDoesNotMatchTarget()
        {
            var collection = BuildCollection(("goblin", 60), ("goblin", 30));
            var rule = new SumEqualsRule("weight", target: 100, groupByField: "table");

            var issues = rule.Evaluate(collection).ToList();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].RecordId, Is.EqualTo("goblin"));
            Assert.That(issues[0].Severity, Is.EqualTo(ValidationSeverity.Error));
        }

        [Test]
        public void Evaluate_UngroupedSumsAcrossWholeCollection_WhenGroupByFieldOmitted()
        {
            var collection = BuildCollection(("goblin", 60), ("wolf", 40));
            var rule = new SumEqualsRule("weight", target: 100);

            var issues = rule.Evaluate(collection).ToList();

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Evaluate_RespectsTolerance()
        {
            var collection = BuildCollection(("goblin", 99.995));
            var rule = new SumEqualsRule("weight", target: 100, tolerance: 0.01);

            var issues = rule.Evaluate(collection).ToList();

            Assert.That(issues, Is.Empty);
        }

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
    }
}
