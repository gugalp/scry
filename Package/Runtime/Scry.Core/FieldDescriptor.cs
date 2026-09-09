using System;

namespace Scry.Core
{
    public sealed class FieldDescriptor
    {
        public string Name { get; }
        public FieldType Type { get; }
        public Schema ElementSchema { get; }
        public bool IsSupported => Type != FieldType.Unsupported;

        public FieldDescriptor(string name, FieldType type, Schema elementSchema = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Field name must not be empty.", nameof(name));

            Name = name;
            Type = type;
            ElementSchema = elementSchema;
        }
    }
}
