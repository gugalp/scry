using System;
using System.Collections.Generic;
using System.Linq;
using Scry.Core;
using UnityEditor;
using UnityEngine;

namespace Scry.Core.Unity
{
    public sealed class ScriptableObjectRepository
    {
        public DataCollection Scan(Type scriptableObjectType)
        {
            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var guids = AssetDatabase.FindAssets($"t:{scriptableObjectType.Name}");
            var records = new List<DataRecord>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
                if (asset == null)
                    continue;

                var serializedObject = new SerializedObject(asset);
                var values = new Dictionary<string, object>();

                foreach (var field in schema.Fields)
                {
                    var property = serializedObject.FindProperty(field.Name);
                    values[field.Name] = field.Type == FieldType.Collection
                        ? (object)ReadCollectionValue(property, field.ElementSchema, guid)
                        : ReadValue(property, field.Type);
                }

                var fingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
                records.Add(new DataRecord(guid, values, fingerprint));
            }

            return new DataCollection(schema, records);
        }

        public DataRecord ApplyEdit(DataRecord record, string fieldName, object value, Type scriptableObjectType)
        {
            var path = ResolveAndCheckFingerprint(record);

            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var fieldDescriptor = schema.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (fieldDescriptor == null)
                throw new InvalidOperationException($"Field '{fieldName}' is not part of the schema for '{scriptableObjectType.Name}'.");

            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
            if (asset == null)
                throw new InvalidOperationException($"Asset for record '{record.Id}' could not be loaded.");

            var serializedObject = new SerializedObject(asset);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found on asset '{path}'.");

            WriteValue(property, value);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var updatedValues = new Dictionary<string, object>(record.Values)
            {
                [fieldName] = ReadValue(property, fieldDescriptor.Type)
            };
            var freshFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return new DataRecord(record.Id, updatedValues, freshFingerprint);
        }

        public DataRecord ApplyEdit(DataRecord record, string collectionField, int index, string childFieldName, object value, Type scriptableObjectType)
        {
            var path = ResolveAndCheckFingerprint(record);

            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var fieldDescriptor = ResolveCollectionField(schema, collectionField);

            var childDescriptor = fieldDescriptor.ElementSchema.Fields.FirstOrDefault(f => f.Name == childFieldName);
            if (childDescriptor == null)
                throw new InvalidOperationException($"Field '{childFieldName}' is not part of the element schema for '{collectionField}'.");

            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
            if (asset == null)
                throw new InvalidOperationException($"Asset for record '{record.Id}' could not be loaded.");

            var serializedObject = new SerializedObject(asset);
            var arrayProperty = serializedObject.FindProperty(collectionField);
            if (arrayProperty == null)
                throw new InvalidOperationException($"Field '{collectionField}' not found on asset '{path}'.");

            if (index < 0 || index >= arrayProperty.arraySize)
                throw new InvalidOperationException($"Index {index} is out of range for '{collectionField}' (size {arrayProperty.arraySize}).");

            var elementProperty = arrayProperty.GetArrayElementAtIndex(index);
            var childProperty = elementProperty.FindPropertyRelative(childFieldName);
            if (childProperty == null)
                throw new InvalidOperationException($"Field '{childFieldName}' not found on element {index} of '{collectionField}'.");

            WriteValue(childProperty, value);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var updatedValues = new Dictionary<string, object>(record.Values)
            {
                [collectionField] = ReadCollectionValue(serializedObject.FindProperty(collectionField), fieldDescriptor.ElementSchema, record.Id)
            };
            var freshFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return new DataRecord(record.Id, updatedValues, freshFingerprint);
        }

        public DataRecord AddCollectionEntry(DataRecord record, string collectionField, Type scriptableObjectType)
        {
            var path = ResolveAndCheckFingerprint(record);

            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var fieldDescriptor = ResolveCollectionField(schema, collectionField);

            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
            if (asset == null)
                throw new InvalidOperationException($"Asset for record '{record.Id}' could not be loaded.");

            var serializedObject = new SerializedObject(asset);
            var arrayProperty = serializedObject.FindProperty(collectionField);
            if (arrayProperty == null)
                throw new InvalidOperationException($"Field '{collectionField}' not found on asset '{path}'.");

            var newIndex = arrayProperty.arraySize;
            arrayProperty.InsertArrayElementAtIndex(newIndex);

            // InsertArrayElementAtIndex duplicates the previous last element's values on a
            // non-empty array, rather than inserting a blank one - reset each field explicitly
            // so a newly added row starts empty instead of cloning the row above it.
            var newElement = arrayProperty.GetArrayElementAtIndex(newIndex);
            foreach (var elementField in fieldDescriptor.ElementSchema.Fields)
                ResetToDefault(newElement.FindPropertyRelative(elementField.Name));

            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var updatedValues = new Dictionary<string, object>(record.Values)
            {
                [collectionField] = ReadCollectionValue(serializedObject.FindProperty(collectionField), fieldDescriptor.ElementSchema, record.Id)
            };
            var freshFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return new DataRecord(record.Id, updatedValues, freshFingerprint);
        }

        public DataRecord RemoveCollectionEntry(DataRecord record, string collectionField, int index, Type scriptableObjectType)
        {
            var path = ResolveAndCheckFingerprint(record);

            var schema = SchemaMapper.InferSchema(scriptableObjectType);
            var fieldDescriptor = ResolveCollectionField(schema, collectionField);

            var asset = AssetDatabase.LoadAssetAtPath(path, scriptableObjectType) as ScriptableObject;
            if (asset == null)
                throw new InvalidOperationException($"Asset for record '{record.Id}' could not be loaded.");

            var serializedObject = new SerializedObject(asset);
            var arrayProperty = serializedObject.FindProperty(collectionField);
            if (arrayProperty == null)
                throw new InvalidOperationException($"Field '{collectionField}' not found on asset '{path}'.");

            if (index < 0 || index >= arrayProperty.arraySize)
                throw new InvalidOperationException($"Index {index} is out of range for '{collectionField}' (size {arrayProperty.arraySize}).");

            // A plain [Serializable] array element (not an object reference or managed reference) is
            // removed by a single DeleteArrayElementAtIndex call - the "call it twice" caveat in Unity's
            // docs only applies to object-reference array elements, which Collection fields never are.
            arrayProperty.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var updatedValues = new Dictionary<string, object>(record.Values)
            {
                [collectionField] = ReadCollectionValue(serializedObject.FindProperty(collectionField), fieldDescriptor.ElementSchema, record.Id)
            };
            var freshFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            return new DataRecord(record.Id, updatedValues, freshFingerprint);
        }

        private static void ResetToDefault(SerializedProperty property)
        {
            // A field can legitimately have no backing SerializedProperty (e.g. a [NonSerialized]
            // field on the element class) even though it appears in the schema - skip it silently
            // rather than crash, matching the "degrade gracefully" spirit used elsewhere for
            // unsupported fields.
            if (property == null)
                return;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = 0;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = false;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                // Property types not covered above (e.g. Vector3, Color, AnimationCurve) are left as
                // InsertArrayElementAtIndex cloned them from the previous row - there's no generic
                // "zero value" to reset them to, so a newly added row's unsupported-typed fields will
                // show the previous row's values rather than a blank default. Documented limitation.
                default:
                    break;
            }
        }

        private static string ResolveAndCheckFingerprint(DataRecord record)
        {
            if (record.Fingerprint == null)
                throw new ArgumentException("record.Fingerprint is null; conflict detection requires a record produced by Scan.", nameof(record));

            var path = AssetDatabase.GUIDToAssetPath(record.Id);
            var currentFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
            if (currentFingerprint != record.Fingerprint)
                throw new WriteConflictException(record.Id);

            return path;
        }

        private static FieldDescriptor ResolveCollectionField(Schema schema, string collectionFieldName)
        {
            var fieldDescriptor = schema.Fields.FirstOrDefault(f => f.Name == collectionFieldName);
            if (fieldDescriptor == null || fieldDescriptor.Type != FieldType.Collection)
                throw new InvalidOperationException($"Field '{collectionFieldName}' is not a Collection field.");

            return fieldDescriptor;
        }

        private static List<DataRecord> ReadCollectionValue(SerializedProperty arrayProperty, Schema elementSchema, string parentRecordId)
        {
            var entries = new List<DataRecord>();
            if (arrayProperty == null || !arrayProperty.isArray)
                return entries;

            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                var elementProperty = arrayProperty.GetArrayElementAtIndex(i);
                var values = new Dictionary<string, object>();

                foreach (var field in elementSchema.Fields)
                {
                    var childProperty = elementProperty.FindPropertyRelative(field.Name);
                    values[field.Name] = ReadValue(childProperty, field.Type);
                }

                entries.Add(new DataRecord($"{parentRecordId}#{i}", values));
            }

            return entries;
        }

        private static void WriteValue(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = (UnityEngine.Object)value;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported property type '{property.propertyType}'.");
            }
        }

        internal static object ReadValue(SerializedProperty property, FieldType type)
        {
            if (property == null)
                return null;

            switch (type)
            {
                case FieldType.Numeric:
                    return property.propertyType == SerializedPropertyType.Integer
                        ? (object)property.intValue
                        : property.floatValue;
                case FieldType.String:
                    return property.stringValue;
                case FieldType.Boolean:
                    return property.boolValue;
                case FieldType.Enum:
                    return property.enumValueIndex;
                case FieldType.Reference:
                    return property.objectReferenceValue;
                default:
                    return null;
            }
        }
    }
}
