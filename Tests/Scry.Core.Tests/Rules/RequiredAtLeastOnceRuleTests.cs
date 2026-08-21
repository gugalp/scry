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
    }
}
