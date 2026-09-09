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
