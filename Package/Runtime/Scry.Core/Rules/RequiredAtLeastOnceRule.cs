using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core.Rules
{
    public sealed class RequiredAtLeastOnceRule : ValidationRule
    {
        private readonly string _field;
        private readonly object _requiredValue;

        public RequiredAtLeastOnceRule(string field, object requiredValue)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _requiredValue = requiredValue;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            var found = collection.Records.Any(r => Equals(r.GetValue(_field), _requiredValue));

            if (!found)
            {
                yield return new ValidationIssue(
                    recordId: null,
                    fieldName: _field,
                    message: $"No record has '{_field}' equal to '{_requiredValue}'; at least one is required.");
            }
        }
    }
}
