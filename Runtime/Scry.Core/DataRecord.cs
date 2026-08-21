using System;
using System.Collections.Generic;

namespace Scry.Core
{
    public sealed class DataRecord
    {
        public string Id { get; }
        public string Fingerprint { get; }
        public IReadOnlyDictionary<string, object> Values => _values;

        private readonly Dictionary<string, object> _values;

        public DataRecord(string id, IReadOnlyDictionary<string, object> values, string fingerprint = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Record id must not be empty.", nameof(id));

            Id = id;
            Fingerprint = fingerprint;
            _values = new Dictionary<string, object>(values ?? throw new ArgumentNullException(nameof(values)));
        }

        public object GetValue(string fieldName)
        {
            return _values.TryGetValue(fieldName, out var value) ? value : null;
        }
    }
}
