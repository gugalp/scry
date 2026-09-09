using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry.Core.Unity.Config
{
    [Serializable]
    public sealed class TrackedCollection
    {
        [SerializeField] private string typeName;
        [SerializeReference] private List<RuleConfig> rules = new List<RuleConfig>();

        public string TypeName => typeName;
        public IReadOnlyList<RuleConfig> Rules => rules;
    }
}
