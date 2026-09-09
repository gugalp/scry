using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Scry.Core.Unity.Tests.Fixtures;

namespace Scry.Core.Unity.Tests
{
    public class ScryConfigSyncTests
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
        public void GetMissingTrackedTypes_ReturnsAttributedType_WhenNotAlreadyTracked()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            AssetDatabase.CreateAsset(config, $"{FixtureFolder}/Config.asset");

            var missing = ScryConfigSync.GetMissingTrackedTypes(config.TrackedCollections).ToList();

            Assert.Contains(typeof(TestScryCollectionAttributedData), missing);
        }

        [Test]
        public void SyncTrackedTypes_AddsMissingAttributedTypes_AsTrackedCollections()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            var path = $"{FixtureFolder}/Config.asset";
            AssetDatabase.CreateAsset(config, path);

            var serializedObject = new SerializedObject(config);
            ScryConfigSync.SyncTrackedTypes(serializedObject);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var reloaded = AssetDatabase.LoadAssetAtPath<ScryConfig>(path);

            Assert.IsTrue(reloaded.TrackedCollections.Any(t => t.TypeName == typeof(TestScryCollectionAttributedData).AssemblyQualifiedName));
        }

        [Test]
        public void SyncTrackedTypes_DoesNotDuplicate_WhenTypeAlreadyTracked()
        {
            var config = ScriptableObject.CreateInstance<ScryConfig>();
            var path = $"{FixtureFolder}/Config.asset";
            AssetDatabase.CreateAsset(config, path);

            var serializedObject = new SerializedObject(config);
            ScryConfigSync.SyncTrackedTypes(serializedObject);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var secondPass = new SerializedObject(config);
            ScryConfigSync.SyncTrackedTypes(secondPass);
            secondPass.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var reloaded = AssetDatabase.LoadAssetAtPath<ScryConfig>(path);
            var matches = reloaded.TrackedCollections.Count(t => t.TypeName == typeof(TestScryCollectionAttributedData).AssemblyQualifiedName);

            Assert.AreEqual(1, matches);
        }
    }
}
