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
            if (record.Fingerprint == null)
                throw new ArgumentException("record.Fingerprint is null; conflict detection requires a record produced by Scan.", nameof(record));

            var path = AssetDatabase.GUIDToAssetPath(record.Id);
            var currentFingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();

            if (currentFingerprint != record.Fingerprint)
                throw new WriteConflictException(record.Id);

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
