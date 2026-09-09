using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core.Rules
{
    public sealed class SumEqualsRule : ValidationRule
    {
        private const string UngroupedKey = "*";

        private readonly string _field;
        private readonly double _target;
        private readonly double _tolerance;
        private readonly string _groupByField;
        private readonly string _nestedField;

        public SumEqualsRule(string field, double target, double tolerance = 0.0001, string groupByField = null, string nestedField = null)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _target = target;
            _tolerance = tolerance;
            _groupByField = groupByField;
            _nestedField = nestedField;
        }

        public override IEnumerable<ValidationIssue> Evaluate(DataCollection collection)
        {
            if (_nestedField == null)
            {
                foreach (var issue in EvaluateGroups(collection.Records, groupKey => groupKey))
                    yield return issue;
                yield break;
            }

            foreach (var parent in collection.Records)
            {
                var nested = parent.GetValue(_nestedField) as IReadOnlyList<DataRecord>;
                if (nested == null)
                    continue;

                foreach (var issue in EvaluateGroups(nested, groupKey => groupKey == UngroupedKey ? parent.Id : $"{parent.Id}/{groupKey}"))
                    yield return issue;
            }
        }

        private IEnumerable<ValidationIssue> EvaluateGroups(IEnumerable<DataRecord> records, Func<string, string> recordIdSelector)
        {
            var groups = _groupByField == null
                ? new[] { (Key: UngroupedKey, Records: records) }
                : records
                    .GroupBy(r => Convert.ToString(r.GetValue(_groupByField)))
                    .Select(g => (Key: g.Key, Records: (IEnumerable<DataRecord>)g))
                    .ToArray();

            foreach (var group in groups)
            {
                var sum = group.Records.Sum(r => Convert.ToDouble(r.GetValue(_field) ?? 0));

                if (Math.Abs(sum - _target) > _tolerance)
                {
                    yield return new ValidationIssue(
                        recordId: recordIdSelector(group.Key),
                        fieldName: _field,
                        message: $"Sum of '{_field}' in group '{group.Key}' is {sum}, expected {_target}.");
                }
            }
        }
    }
}
