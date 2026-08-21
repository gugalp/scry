using System;
using System.Collections.Generic;

namespace Scry.Core.Rules
{
    public sealed class NoDuplicateRule : ValidationRule
    {
        private readonly string _field;

        public NoDuplicateRule(string field)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            var seen = new Dictionary<object, string>();

            foreach (var record in collection.Records)
            {
                var value = record.GetValue(_field);
                if (value == null)
                    continue;

                if (seen.TryGetValue(value, out var firstRecordId))
                {
                    yield return new ValidationIssue(
                        recordId: record.Id,
                        fieldName: _field,
                        message: $"Duplicate value '{value}' for '{_field}' (already used by record '{firstRecordId}').");
                }
                else
                {
                    seen[value] = record.Id;
                }
            }
        }
    }
}
