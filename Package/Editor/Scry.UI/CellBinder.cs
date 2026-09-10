using System;
using Scry.Core;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Scry.UI
{
    public static class CellBinder
    {
        public static VisualElement CreateCell(FieldDescriptor field)
        {
            switch (field.Type)
            {
                case FieldType.Numeric:
                    var floatField = new FloatField { isDelayed = true };
                    floatField.RegisterValueChangedCallback(evt => Invoke(floatField, evt.newValue));
                    return floatField;
                case FieldType.String:
                    var textField = new TextField { isDelayed = true };
                    textField.RegisterValueChangedCallback(evt => Invoke(textField, evt.newValue));
                    return textField;
                case FieldType.Boolean:
                    var toggle = new Toggle();
                    toggle.RegisterValueChangedCallback(evt => Invoke(toggle, evt.newValue));
                    return toggle;
                case FieldType.Enum:
                    var intField = new IntegerField { isDelayed = true };
                    intField.RegisterValueChangedCallback(evt => Invoke(intField, evt.newValue));
                    return intField;
                case FieldType.Reference:
                    var objectField = new ObjectField();
                    objectField.RegisterValueChangedCallback(evt => Invoke(objectField, evt.newValue));
                    return objectField;
                default:
                    return new Label();
            }
        }

        public static void BindCell(VisualElement cell, FieldDescriptor field, DataRecord record, Action<object> onValueChanged)
        {
            var value = record.GetValue(field.Name);

            switch (field.Type)
            {
                case FieldType.Numeric:
                    var floatField = (FloatField)cell;
                    floatField.SetValueWithoutNotify(Convert.ToSingle(value ?? 0f));
                    floatField.userData = onValueChanged;
                    break;
                case FieldType.String:
                    var textField = (TextField)cell;
                    textField.SetValueWithoutNotify((string)value ?? string.Empty);
                    textField.userData = onValueChanged;
                    break;
                case FieldType.Boolean:
                    var toggle = (Toggle)cell;
                    toggle.SetValueWithoutNotify(value is bool b && b);
                    toggle.userData = onValueChanged;
                    break;
                case FieldType.Enum:
                    var intField = (IntegerField)cell;
                    intField.SetValueWithoutNotify(Convert.ToInt32(value ?? 0));
                    intField.userData = onValueChanged;
                    break;
                case FieldType.Reference:
                    var objectField = (ObjectField)cell;
                    objectField.SetValueWithoutNotify(value as UnityEngine.Object);
                    objectField.userData = onValueChanged;
                    break;
                case FieldType.Collection:
                    var entries = value as System.Collections.Generic.IReadOnlyList<DataRecord>;
                    ((Label)cell).text = $"{entries?.Count ?? 0} entries";
                    break;
                default:
                    ((Label)cell).text = "(unsupported)";
                    break;
            }
        }

        // The change-callback is registered exactly once, when the control is created
        // (CreateCell) - not on every BindCell call, which happens repeatedly on the SAME
        // element as MultiColumnTreeView reuses it across virtualized rows. BindCell only ever
        // swaps which Action<object> is currently stored in userData, so a rebind never touches
        // Unity's event system and there is never more than one registered handler to begin
        // with - "no stacking" is provable by inspecting userData directly (see
        // CellBinderTests), without needing a live UI Toolkit panel to observe event dispatch
        // (unavailable in headless batch-mode EditMode tests).
        private static void Invoke(VisualElement field, object newValue)
        {
            if (field.userData is Action<object> callback)
                callback(newValue);
        }
    }
}
