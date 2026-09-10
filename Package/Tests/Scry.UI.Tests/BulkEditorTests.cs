// Package/Tests/Scry.UI.Tests/BulkEditorTests.cs
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scry.Core;
using Scry.Core.Unity;
using Scry.UI.Tests.Fixtures;
using UnityEditor;
using UnityEngine;

namespace Scry.UI.Tests
{
    public class BulkEditorTests
    {
        private const string FixtureFolder = "Assets/ScryUiTestFixtures";
        private ScriptableObjectRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _repository = new ScriptableObjectRepository();
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
                AssetDatabase.CreateFolder("Assets", "ScryUiTestFixtures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }

        [Test]
        public void ApplyToSelected_WritesValueToEveryRecord()
        {
            var itemA = ScriptableObject.CreateInstance<TestUiItemData>();
            itemA.itemName = "A";
            AssetDatabase.CreateAsset(itemA, $"{FixtureFolder}/A.asset");
            var itemB = ScriptableObject.CreateInstance<TestUiItemData>();
            itemB.itemName = "B";
            AssetDatabase.CreateAsset(itemB, $"{FixtureFolder}/B.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestUiItemData));
            var updatedRecords = new List<DataRecord>();

            BulkEditor.ApplyToSelected(_repository, collection.Records, "weight", 9f, typeof(TestUiItemData), r => updatedRecords.Add(r));

            Assert.AreEqual(2, updatedRecords.Count);
            Assert.IsTrue(updatedRecords.All(r => (float)r.GetValue("weight") == 9f));
        }

        [Test]
        public void ApplyToSelected_CollapsesAllEditsIntoOneUndoGroup()
        {
            var itemA = ScriptableObject.CreateInstance<TestUiItemData>();
            itemA.itemName = "A";
            AssetDatabase.CreateAsset(itemA, $"{FixtureFolder}/A.asset");
            var itemB = ScriptableObject.CreateInstance<TestUiItemData>();
            itemB.itemName = "B";
            AssetDatabase.CreateAsset(itemB, $"{FixtureFolder}/B.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestUiItemData));

            Undo.IncrementCurrentGroup();
            var groupBeforeBulkEdit = Undo.GetCurrentGroup();

            BulkEditor.ApplyToSelected(_repository, collection.Records, "weight", 9f, typeof(TestUiItemData), _ => { });

            var afterBulkEdit = _repository.Scan(typeof(TestUiItemData));
            Assert.IsTrue(afterBulkEdit.Records.All(r => (float)r.GetValue("weight") == 9f));

            Undo.RevertAllDownToGroup(groupBeforeBulkEdit);

            var afterUndo = _repository.Scan(typeof(TestUiItemData));
            Assert.IsTrue(afterUndo.Records.All(r => (float)r.GetValue("weight") == 0f));
        }
    }
}
