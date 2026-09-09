using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scry.Core.Rules;

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

            Assert.That(rule.Evaluate(collection), Is.Empty);
        }

        [Test]
        public void Evaluate_ReportsIssue_OnDuplicateValue()
        {
            var collection = BuildCollection(("r1", "sword"), ("r2", "sword"));
            var rule = new NoDuplicateRule("itemId");

            var issues = rule.Evaluate(collection).ToList();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].RecordId, Is.EqualTo("r2"));
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

            Assert.That(rule.Evaluate(collection), Is.Empty);
        }

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
    }
}
