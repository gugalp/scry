using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public class TestBaseData : ScriptableObject
    {
        [SerializeField] private int baseId;

        public int BaseId => baseId;
    }
}
