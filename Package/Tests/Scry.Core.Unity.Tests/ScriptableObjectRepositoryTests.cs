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

        [Test]
        public void ApplyEdit_WritesValueBackToAsset()
        {
            var item = ScriptableObject.CreateInstance<TestItemData>();
            item.itemName = "Rusty Sword";
            var path = $"{FixtureFolder}/RustySword.asset";
            AssetDatabase.CreateAsset(item, path);
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestItemData));
            var record = collection.Records[0];

            var updated = _repository.ApplyEdit(record, "itemName", "Legendary Sword", typeof(TestItemData));

            Assert.AreEqual("Legendary Sword", updated.GetValue("itemName"));

            var reloaded = _repository.Scan(typeof(TestItemData));
            Assert.AreEqual("Legendary Sword", reloaded.Records[0].GetValue("itemName"));
        }

        [Test]
        public void ApplyEdit_ReturnsRecordWithFreshFingerprint_AllowingConsecutiveEditsWithoutRescan()
        {
            var item = ScriptableObject.CreateInstance<TestItemData>();
            item.itemName = "Rusty Sword";
            item.weight = 1;
            var path = $"{FixtureFolder}/RustySword.asset";
            AssetDatabase.CreateAsset(item, path);
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestItemData));
            var record = collection.Records[0];

            var afterFirstEdit = _repository.ApplyEdit(record, "itemName", "Legendary Sword", typeof(TestItemData));
            var afterSecondEdit = _repository.ApplyEdit(afterFirstEdit, "weight", 9, typeof(TestItemData));

            Assert.AreEqual("Legendary Sword", afterSecondEdit.GetValue("itemName"));
            Assert.AreEqual(9, afterSecondEdit.GetValue("weight"));
        }

        [Test]
        public void ApplyEdit_ThrowsArgumentException_WhenRecordHasNullFingerprint()
        {
            var item = ScriptableObject.CreateInstance<TestItemData>();
            item.itemName = "Rusty Sword";
            var path = $"{FixtureFolder}/RustySword.asset";
            AssetDatabase.CreateAsset(item, path);
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestItemData));
            var scannedRecord = collection.Records[0];
            var recordWithoutFingerprint = new DataRecord(scannedRecord.Id, scannedRecord.Values);

            Assert.Throws<System.ArgumentException>(() =>
                _repository.ApplyEdit(recordWithoutFingerprint, "itemName", "Sneaky Overwrite", typeof(TestItemData)));

            var rescanned = _repository.Scan(typeof(TestItemData));
            Assert.AreEqual("Rusty Sword", rescanned.Records[0].GetValue("itemName"));
        }

        [Test]
        public void ApplyEdit_ThrowsWriteConflict_WhenAssetChangedExternallySinceScan()
        {
            var item = ScriptableObject.CreateInstance<TestItemData>();
            item.itemName = "Rusty Sword";
            var path = $"{FixtureFolder}/RustySword.asset";
            AssetDatabase.CreateAsset(item, path);
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestItemData));
            var record = collection.Records[0];

            var externallyLoaded = AssetDatabase.LoadAssetAtPath<TestItemData>(path);
            externallyLoaded.itemName = "Changed By Someone Else";
            EditorUtility.SetDirty(externallyLoaded);
            AssetDatabase.SaveAssets();

            Assert.Throws<WriteConflictException>(() =>
                _repository.ApplyEdit(record, "itemName", "My Edit", typeof(TestItemData)));
        }

        [Test]
        public void Scan_ReadsCollectionField_AsNestedDataRecords()
        {
            var monster = ScriptableObject.CreateInstance<TestMonsterData>();
            monster.monsterName = "Goblin";
            monster.dropTable = new List<TestDropEntry>
            {
                new TestDropEntry { itemId = "sword", weight = 60f },
                new TestDropEntry { itemId = "shield", weight = 40f }
            };
            AssetDatabase.CreateAsset(monster, $"{FixtureFolder}/Goblin.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestMonsterData));

            var record = collection.Records[0];
            var dropTable = record.GetValue("dropTable") as IReadOnlyList<DataRecord>;

            Assert.IsNotNull(dropTable);
            Assert.AreEqual(2, dropTable.Count);
            Assert.AreEqual("sword", dropTable[0].GetValue("itemId"));
            Assert.AreEqual(60f, dropTable[0].GetValue("weight"));
            Assert.AreEqual($"{record.Id}#0", dropTable[0].Id);
        }

        [Test]
        public void Scan_ReadsEmptyCollectionField_AsEmptyList()
        {
            var monster = ScriptableObject.CreateInstance<TestMonsterData>();
            AssetDatabase.CreateAsset(monster, $"{FixtureFolder}/EmptyGoblin.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestMonsterData));

            var dropTable = collection.Records[0].GetValue("dropTable") as IReadOnlyList<DataRecord>;

            Assert.IsNotNull(dropTable);
            Assert.IsEmpty(dropTable);
        }
    }
}
