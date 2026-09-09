using System;
using System.Collections.Generic;
using System.Linq;
using Scry.Core.Unity.Config;
using UnityEditor;

namespace Scry.Core.Unity
{
    public static class ScryConfigSync
    {
        public static IEnumerable<Type> GetMissingTrackedTypes(IReadOnlyList<TrackedCollection> existing)
        {
            var existingTypeNames = new HashSet<string>(existing.Select(t => t.TypeName));

            foreach (var type in TypeCache.GetTypesWithAttribute<ScryCollectionAttribute>())
            {
                if (!existingTypeNames.Contains(type.AssemblyQualifiedName))
                    yield return type;
            }
        }

        public static void SyncTrackedTypes(SerializedObject configSerializedObject)
        {
            var config = (ScryConfig)configSerializedObject.targetObject;
            var missing = GetMissingTrackedTypes(config.TrackedCollections).ToList();
            if (missing.Count == 0)
                return;

            var trackedProperty = configSerializedObject.FindProperty("trackedCollections");
            foreach (var type in missing)
            {
                var index = trackedProperty.arraySize;
                trackedProperty.InsertArrayElementAtIndex(index);
                trackedProperty.GetArrayElementAtIndex(index).FindPropertyRelative("typeName").stringValue = type.AssemblyQualifiedName;
            }
        }
    }
}
