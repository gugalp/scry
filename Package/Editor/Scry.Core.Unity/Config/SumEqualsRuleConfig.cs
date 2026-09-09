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
