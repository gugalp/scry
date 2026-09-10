// Package/Editor/Scry.UI/BulkEditor.cs
using System;
using System.Collections.Generic;
using Scry.Core;
using Scry.Core.Unity;
using UnityEditor;

namespace Scry.UI
{
    public static class BulkEditor
    {
        public static void ApplyToSelected(ScriptableObjectRepository repository, IEnumerable<DataRecord> selectedRecords, string fieldName, object value, Type scriptableObjectType, Action<DataRecord> onRecordUpdated)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Bulk edit '{fieldName}'");
            var undoGroup = Undo.GetCurrentGroup();

            try
            {
                foreach (var record in selectedRecords)
                {
                    var updated = EditGateway.ApplyEdit(repository, record, fieldName, value, scriptableObjectType, $"Bulk edit '{fieldName}'");
                    onRecordUpdated(updated);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
                Undo.IncrementCurrentGroup();
            }
        }
    }
}
