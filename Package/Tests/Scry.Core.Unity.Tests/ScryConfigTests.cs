using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Scry.Core.Unity.Config;

namespace Scry.Core.Unity.Tests
{
    public class ScryConfigTests
    {
        private const string FixtureFolder = "Assets/ScryTestFixtures";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
                AssetDatabase.CreateFolder("Assets", "ScryTestFixtures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }

        [Test]
        public void ScryConfig_RoundTripsSerializeReferenceRuleConfigs_ThroughAssetSaveAndReload()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            var path = $"{FixtureFolder}/TestConfig.asset";
            AssetDatabase.CreateAsset(config, path);

            var serializedObject = new SerializedObject(config);
            var tracked = serializedObject.FindProperty("trackedCollections");
            tracked.InsertArrayElementAtIndex(0);
            var trackedElement = tracked.GetArrayElementAtIndex(0);
            trackedElement.FindPropertyRelative("typeName").stringValue = "TestMonsterData";
            var rules = trackedElement.FindPropertyRelative("rules");
            rules.InsertArrayElementAtIndex(0);
            var ruleElement = rules.GetArrayElementAtIndex(0);
            ruleElement.managedReferenceValue = new SumEqualsRuleConfig { Field = "weight", Target = 100, GroupByField = "table" };
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reloaded = AssetDatabase.LoadAssetAtPath<ScryConfig>(path);
            var reloadedSerializedObject = new SerializedObject(reloaded);
            var reloadedRule = reloadedSerializedObject.FindProperty("trackedCollections")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("rules")
                .GetArrayElementAtIndex(0)
                .managedReferenceValue;

            Assert.IsInstanceOf<SumEqualsRuleConfig>(reloadedRule);
            var reloadedSumEquals = (SumEqualsRuleConfig)reloadedRule;
            Assert.AreEqual("weight", reloadedSumEquals.Field);
            Assert.AreEqual(100, reloadedSumEquals.Target);
            Assert.AreEqual("table", reloadedSumEquals.GroupByField);
        }

        [Test]
        public void ScryConfig_TrackedCollectionsExposesTypeNameAndRules_ViaPublicApi()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            var path = $"{FixtureFolder}/TestConfig2.asset";
            AssetDatabase.CreateAsset(config, path);

            var serializedObject = new SerializedObject(config);
            var tracked = serializedObject.FindProperty("trackedCollections");
            tracked.InsertArrayElementAtIndex(0);
            tracked.GetArrayElementAtIndex(0).FindPropertyRelative("typeName").stringValue = "TestMonsterData";
            serializedObject.ApplyModifiedProperties();

            Assert.AreEqual(1, config.TrackedCollections.Count);
            Assert.AreEqual("TestMonsterData", config.TrackedCollections[0].TypeName);
            Assert.IsEmpty(config.TrackedCollections[0].Rules);
        }
    }
}
