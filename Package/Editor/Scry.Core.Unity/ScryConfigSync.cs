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

                // InsertArrayElementAtIndex duplicates the previous last element's values on a
                // non-empty array (see ScriptableObjectRepository.AddCollectionEntry's comment for
                // the same Unity quirk) - clear the copied rules before setting the new type name,
                // otherwise the new entry silently inherits the previous entry's validation rules.
                var element = trackedProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("rules").ClearArray();
                element.FindPropertyRelative("typeName").stringValue = type.AssemblyQualifiedName;
            }
        }
    }
}
