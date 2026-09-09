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
