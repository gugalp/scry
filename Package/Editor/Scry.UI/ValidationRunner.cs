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
