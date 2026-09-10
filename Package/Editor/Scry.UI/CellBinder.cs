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
                    return new FloatField { isDelayed = true };
                case FieldType.String:
                    return new TextField { isDelayed = true };
                case FieldType.Boolean:
                    return new Toggle();
                case FieldType.Enum:
                    return new IntegerField { isDelayed = true };
                case FieldType.Reference:
                    return new ObjectField();
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
                    BindNotifyingField((FloatField)cell, Convert.ToSingle(value ?? 0f), onValueChanged);
                    break;
                case FieldType.String:
                    BindNotifyingField((TextField)cell, (string)value ?? string.Empty, onValueChanged);
                    break;
                case FieldType.Boolean:
                    BindNotifyingField((Toggle)cell, value is bool b && b, onValueChanged);
                    break;
                case FieldType.Enum:
                    BindNotifyingField((IntegerField)cell, Convert.ToInt32(value ?? 0), onValueChanged);
                    break;
                case FieldType.Reference:
                    BindNotifyingField((ObjectField)cell, value as UnityEngine.Object, onValueChanged);
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

        // A field/toggle/object-field control is reused across virtualized rows, so BindCell is
        // called repeatedly on the SAME element for different records as the grid scrolls.
        // Registering a new callback every time without unregistering the previous one would
        // stack handlers and fire an edit multiple times - store the current callback on the
        // element and unregister it before adding the new one.
        private static void BindNotifyingField<TValue>(BaseField<TValue> field, TValue value, Action<object> onValueChanged)
        {
            if (field.userData is EventCallback<ChangeEvent<TValue>> previousCallback)
                field.UnregisterValueChangedCallback(previousCallback);

            field.SetValueWithoutNotify(value);

            EventCallback<ChangeEvent<TValue>> callback = evt => onValueChanged(evt.newValue);
            field.userData = callback;
            field.RegisterValueChangedCallback(callback);
        }
    }
}
