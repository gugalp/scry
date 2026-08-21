using System;
using System.Collections.Generic;
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
                    values[field.Name] = ReadValue(property, field.Type);
                }

                var fingerprint = AssetDatabase.GetAssetDependencyHash(path).ToString();
                records.Add(new DataRecord(guid, values, fingerprint));
            }

            return new DataCollection(schema, records);
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
