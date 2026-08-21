using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public enum TestRarity
    {
        Common,
        Rare
    }

    public class TestItemData : ScriptableObject
    {
        public string itemName;
        public int weight;
        [SerializeField] private float dropChance;
        public bool isUnique;
        public TestRarity rarity;
        public TestItemData referencedItem;
        public Vector3 unsupportedField;

        public float DropChance => dropChance;
    }
}
