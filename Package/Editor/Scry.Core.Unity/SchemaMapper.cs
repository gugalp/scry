using System;
using System.Collections.Generic;
using System.Reflection;
using Scry.Core;
using UnityEngine;

namespace Scry.Core.Unity
{
    public static class SchemaMapper
    {
        public static Schema InferSchema(Type scriptableObjectType)
        {
            if (scriptableObjectType == null)
                throw new ArgumentNullException(nameof(scriptableObjectType));

            return InferSchema(scriptableObjectType, allowCollectionFields: true);
        }

        private static Schema InferSchema(Type type, bool allowCollectionFields)
        {
            var seenNames = new HashSet<string>();
            var fields = new List<FieldDescriptor>();

            // Public fields, including those inherited from base classes — GetFields already walks
            // the hierarchy for BindingFlags.Public, no manual walk needed here.
            foreach (var member in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (seenNames.Add(member.Name))
                    fields.Add(MapField(member.Name, member.FieldType, allowCollectionFields));
            }

            // Non-public [SerializeField] fields. Unlike Public, BindingFlags.NonPublic only returns
            // fields declared directly on the queried type — it does NOT return private fields
            // declared on base types. Unity still serializes those, so walk the hierarchy ourselves.
            foreach (var member in CollectNonPublicSerializedFields(type))
            {
                if (seenNames.Add(member.Name))
                    fields.Add(MapField(member.Name, member.FieldType, allowCollectionFields));
            }

            return new Schema(type.Name, fields);
        }

        private static IEnumerable<FieldInfo> CollectNonPublicSerializedFields(Type type)
        {
            for (var current = type; current != null && current != typeof(UnityEngine.Object); current = current.BaseType)
            {
                foreach (var member in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (member.GetCustomAttribute<SerializeField>() != null)
                        yield return member;
                }
            }
        }

        private static FieldDescriptor MapField(string name, Type fieldType, bool allowCollectionFields)
        {
            if (allowCollectionFields)
            {
                var elementType = GetCollectionElementType(fieldType);
                if (elementType != null && IsPlainSerializableClass(elementType))
                {
                    // Nested collection schemas never allow further collection fields -
                    // List<List<T>> (or deeper) is out of scope and stays Unsupported instead.
                    var elementSchema = InferSchema(elementType, allowCollectionFields: false);
                    return new FieldDescriptor(name, FieldType.Collection, elementSchema);
                }
            }

            return new FieldDescriptor(name, MapFieldType(fieldType));
        }

        private static Type GetCollectionElementType(Type fieldType)
        {
            if (fieldType.IsArray)
                return fieldType.GetElementType();

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                return fieldType.GetGenericArguments()[0];

            return null;
        }

        private static bool IsPlainSerializableClass(Type type)
        {
            return type.IsClass
                && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                && type.IsDefined(typeof(SerializableAttribute), inherit: false)
                && GetCollectionElementType(type) == null
                // Reject candidate element types MapFieldType already recognizes as a known
                // scalar/reference kind - most notably System.String, which is a [Serializable]
                // class in the BCL and would otherwise be misclassified as a Collection of a
                // zero-field element schema (String has no public/[SerializeField] instance
                // fields), making the string values unreadable/uneditable through this field.
                && MapFieldType(type) == FieldType.Unsupported;
        }

        private static FieldType MapFieldType(Type type)
        {
            if (type == typeof(int) || type == typeof(float))
                return FieldType.Numeric;
            if (type == typeof(string))
                return FieldType.String;
            if (type == typeof(bool))
                return FieldType.Boolean;
            if (type.IsEnum)
                return FieldType.Enum;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return FieldType.Reference;

            return FieldType.Unsupported;
        }
    }
}
