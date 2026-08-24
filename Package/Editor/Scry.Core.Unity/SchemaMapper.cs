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

            var seenNames = new HashSet<string>();
            var fields = new List<FieldDescriptor>();

            // Public fields, including those inherited from base classes — GetFields already walks
            // the hierarchy for BindingFlags.Public, no manual walk needed here.
            foreach (var member in scriptableObjectType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (seenNames.Add(member.Name))
                    fields.Add(new FieldDescriptor(member.Name, MapFieldType(member.FieldType)));
            }

            // Non-public [SerializeField] fields. Unlike Public, BindingFlags.NonPublic only returns
            // fields declared directly on the queried type — it does NOT return private fields
            // declared on base types. Unity still serializes those, so walk the hierarchy ourselves.
            foreach (var member in CollectNonPublicSerializedFields(scriptableObjectType))
            {
                // Derived-class-first: a derived field already recorded above (or from a more-derived
                // level of this walk) shadows a base field of the same name, so skip the base one.
                if (seenNames.Add(member.Name))
                    fields.Add(new FieldDescriptor(member.Name, MapFieldType(member.FieldType)));
            }

            return new Schema(scriptableObjectType.Name, fields);
        }

        private static IEnumerable<FieldInfo> CollectNonPublicSerializedFields(Type scriptableObjectType)
        {
            for (var type = scriptableObjectType; type != null && type != typeof(UnityEngine.Object); type = type.BaseType)
            {
                foreach (var member in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (member.GetCustomAttribute<SerializeField>() != null)
                        yield return member;
                }
            }
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
