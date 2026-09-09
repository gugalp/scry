using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core.Rules
{
    public sealed class RequiredAtLeastOnceRule : ValidationRule
    {
        private readonly string _field;
        private readonly object _requiredValue;
        private readonly string _nestedField;

        public RequiredAtLeastOnceRule(string field, object requiredValue, string nestedField = null)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _requiredValue = requiredValue;
            _nestedField = nestedField;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            if (_nestedField == null)
            {
                var found = collection.Records.Any(r => Equals(r.GetValue(_field), _requiredValue));
                if (!found)
                {
                    yield return new ValidationIssue(
                        recordId: null,
                        fieldName: _field,
                        message: $"No record has '{_field}' equal to '{_requiredValue}'; at least one is required.");
                }
                yield break;
            }

            foreach (var parent in collection.Records)
            {
                var nested = parent.GetValue(_nestedField) as IReadOnlyList<DataRecord>;
                var found = nested != null && nested.Any(r => Equals(r.GetValue(_field), _requiredValue));

                if (!found)
                {
                    yield return new ValidationIssue(
                        recordId: parent.Id,
                        fieldName: _field,
                        message: $"Record '{parent.Id}' has no entry in '{_nestedField}' with '{_field}' equal to '{_requiredValue}'; at least one is required.");
                }
            }
        }
    }
}
