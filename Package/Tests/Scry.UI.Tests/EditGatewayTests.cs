// Package/Tests/Scry.UI.Tests/EditGatewayTests.cs
using NUnit.Framework;
using Scry.Core.Unity;
using Scry.UI.Tests.Fixtures;
using UnityEditor;
using UnityEngine;

namespace Scry.UI.Tests
{
    public class EditGatewayTests
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
        public void ApplyEdit_WritesValueBackToAsset()
        {
            var item = ScriptableObject.CreateInstance<TestUiItemData>();
            item.itemName = "Rusty Sword";
            AssetDatabase.CreateAsset(item, $"{FixtureFolder}/RustySword.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestUiItemData));
            var record = collection.Records[0];

            var updated = EditGateway.ApplyEdit(_repository, record, "itemName", "Legendary Sword", typeof(TestUiItemData), "Edit itemName");

            Assert.AreEqual("Legendary Sword", updated.GetValue("itemName"));
        }

        [Test]
        public void ApplyEdit_RegistersUndo_SoRevertingTheGroupRestoresThePreviousValue()
        {
            var item = ScriptableObject.CreateInstance<TestUiItemData>();
            item.itemName = "Rusty Sword";
            AssetDatabase.CreateAsset(item, $"{FixtureFolder}/RustySword.asset");
            AssetDatabase.SaveAssets();

            var collection = _repository.Scan(typeof(TestUiItemData));
            var record = collection.Records[0];

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            EditGateway.ApplyEdit(_repository, record, "itemName", "Legendary Sword", typeof(TestUiItemData), "Edit itemName");

            var afterEdit = _repository.Scan(typeof(TestUiItemData));
            Assert.AreEqual("Legendary Sword", afterEdit.Records[0].GetValue("itemName"));

            Undo.RevertAllDownToGroup(undoGroup);

            var afterUndo = _repository.Scan(typeof(TestUiItemData));
            Assert.AreEqual("Rusty Sword", afterUndo.Records[0].GetValue("itemName"));
        }
    }
}
