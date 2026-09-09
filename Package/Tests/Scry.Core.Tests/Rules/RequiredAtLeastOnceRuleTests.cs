using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scry.Core.Rules;

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

            Assert.That(rule.Evaluate(collection), Is.Empty);
        }

        [Test]
        public void Evaluate_ReportsIssue_WhenNoRecordMatches()
        {
            var collection = BuildCollection(false, false);
            var rule = new RequiredAtLeastOnceRule("isStarter", true);

            var issues = rule.Evaluate(collection).ToList();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].FieldName, Is.EqualTo("isStarter"));
        }

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
    }
}
