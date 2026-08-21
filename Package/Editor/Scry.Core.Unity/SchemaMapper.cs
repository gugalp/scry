using System;
using System.Collections.Generic;
using System.Linq;
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

            var members = scriptableObjectType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null);

            var fields = members
                .Select(m => new FieldDescriptor(m.Name, MapFieldType(m.FieldType)))
                .ToList();

            return new Schema(scriptableObjectType.Name, fields);
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
