using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core
{
    public sealed class Schema
    {
        public string TypeName { get; }
        public IReadOnlyList<FieldDescriptor> Fields { get; }

        public Schema(string typeName, IEnumerable<FieldDescriptor> fields)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Schema type name must not be empty.", nameof(typeName));

            TypeName = typeName;
            Fields = (fields ?? throw new ArgumentNullException(nameof(fields))).ToList();
        }

        public FieldDescriptor GetField(string name)
        {
            return Fields.FirstOrDefault(f => f.Name == name);
        }
    }
}
