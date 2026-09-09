using System;

namespace Scry.Core.Unity.Config
{
    [Serializable]
    public sealed class RequiredAtLeastOnceRuleConfig : RuleConfig
    {
        public string Field;

        // Compared via Equals against the target field's raw value, so this matches
        // String-typed fields in v1. Enum/Numeric target support needs RuleFactory to see
        // the target Schema at build time, which it doesn't yet - deferred, not forgotten.
        public string RequiredValue;
        public string NestedField;
    }
}
