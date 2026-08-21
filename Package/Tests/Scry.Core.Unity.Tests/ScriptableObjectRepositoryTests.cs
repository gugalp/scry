using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Scry.Core.Unity.Tests.Fixtures;

namespace Scry.Core.Unity.Tests
{
    public class ScriptableObjectRepositoryTests
    {
        private const string FixtureFolder = "Assets/ScryTestFixtures";
        private ScriptableObjectRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _repository = new ScriptableObjectRepository();
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
                AssetDatabase.CreateFolder("Assets", "ScryTestFixtures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }

        [Test]
        public void Scan_ReadsFieldValuesFromRealAssets()
        {
            var item = ScriptableObject.CreateInstance<TestItemData>();
            item.itemName = "Rusty Sword";
            item.weight = 5;
            item.isUnique = true;
            AssetDatabase.CreateAsset(item, $"{FixtureFolder}/RustySword.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestItemData));

            Assert.AreEqual(1, collection.Records.Count);
            var record = collection.Records[0];
            Assert.AreEqual("Rusty Sword", record.GetValue("itemName"));
            Assert.AreEqual(5, record.GetValue("weight"));
            Assert.AreEqual(true, record.GetValue("isUnique"));
        }

        [Test]
        public void Scan_SetsNonNullFingerprintPerRecord()
        {
            var item = ScriptableObject.CreateInstance<TestItemData>();
            AssetDatabase.CreateAsset(item, $"{FixtureFolder}/Fingerprinted.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestItemData));

            Assert.IsNotNull(collection.Records[0].Fingerprint);
        }

        [Test]
        public void Scan_ReturnsEmptyCollection_WhenNoAssetsExist()
        {
            var collection = _repository.Scan(typeof(TestItemData));

            Assert.IsEmpty(collection.Records);
        }
    }
}
