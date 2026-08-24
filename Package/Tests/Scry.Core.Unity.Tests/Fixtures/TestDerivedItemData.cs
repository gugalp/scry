using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public class TestDerivedItemData : TestBaseData
    {
        public string derivedName;
        [SerializeField] private float derivedWeight;

        public float DerivedWeight => derivedWeight;
    }
}
