using System;
using System.Collections.Generic;

namespace Scry.Core.Rules
{
    public sealed class NoDuplicateRule : ValidationRule
    {
        private readonly string _field;
        private readonly string _nestedField;

        public NoDuplicateRule(string field, string nestedField = null)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _nestedField = nestedField;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            if (_nestedField == null)
            {
                foreach (var issue in EvaluateNoDuplicates(collection.Records))
                    yield return issue;
                yield break;
            }

            foreach (var parent in collection.Records)
            {
                var nested = parent.GetValue(_nestedField) as IReadOnlyList<DataRecord>;
                if (nested == null)
                    continue;

                foreach (var issue in EvaluateNoDuplicates(nested))
                    yield return issue;
            }
        }

        private IEnumerable<ValidationIssue> EvaluateNoDuplicates(IEnumerable<DataRecord> records)
        {
            var seen = new Dictionary<object, string>();

            foreach (var record in records)
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
