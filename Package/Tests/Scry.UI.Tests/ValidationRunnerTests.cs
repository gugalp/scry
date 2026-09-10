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
