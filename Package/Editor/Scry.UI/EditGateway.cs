// Package/Editor/Scry.UI/EditGateway.cs
using System;
using Scry.Core;
using Scry.Core.Unity;
using UnityEditor;

namespace Scry.UI
{
    // The single place every DataGridWindow write goes through. Registering Undo here, right
    // before calling into the repository, means every edit path (single cell, nested cell, bulk)
    // gets undo support for free just by routing through this class instead of the repository
    // directly.
    public static class EditGateway
    {
        public static DataRecord ApplyEdit(ScriptableObjectRepository repository, DataRecord record, string fieldName, object value, Type scriptableObjectType, string undoName)
        {
            RegisterUndo(record, scriptableObjectType, undoName);
            return repository.ApplyEdit(record, fieldName, value, scriptableObjectType);
        }

        public static DataRecord ApplyNestedEdit(ScriptableObjectRepository repository, DataRecord record, string collectionField, int index, string childFieldName, object value, Type scriptableObjectType, string undoName)
        {
            RegisterUndo(record, scriptableObjectType, undoName);
            return repository.ApplyEdit(record, collectionField, index, childFieldName, value, scriptableObjectType);
        }

        private static void RegisterUndo(DataRecord record, Type scriptableObjectType, string undoName)
        {
            var path = AssetDatabase.GUIDToAssetPath(record.Id);
            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as UnityEngine.Object;
            if (asset != null)
                Undo.RegisterCompleteObjectUndo(asset, undoName);
        }
    }
}
