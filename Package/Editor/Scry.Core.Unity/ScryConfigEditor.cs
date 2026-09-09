using UnityEditor;
using UnityEngine;

namespace Scry.Core.Unity
{
    [CustomEditor(typeof(ScryConfig))]
    public sealed class ScryConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Sync Tracked Types"))
            {
                ScryConfigSync.SyncTrackedTypes(serializedObject);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
