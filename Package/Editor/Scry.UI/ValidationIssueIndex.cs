using System.Collections.Generic;
using System.Linq;
using Scry.Core;

namespace Scry.UI
{
    public sealed class ValidationIssueIndex
    {
        private readonly ILookup<(string RecordId, string FieldName), ValidationIssue> _byCell;
        private readonly ILookup<(string RecordId, string CollectionFieldName), ValidationIssue> _byCollectionField;

        public IReadOnlyList<ValidationIssue> AllIssues { get; }

        public ValidationIssueIndex(IReadOnlyList<ValidationIssue> issues, Schema schema = null)
        {
            AllIssues = issues;
            _byCell = issues.ToLookup(i => (ValidationIssueLocator.Locate(i).ParentRecordId ?? string.Empty, i.FieldName));
            _byCollectionField = schema != null ? BuildCollectionFieldLookup(issues, schema) : null;
        }

        public IReadOnlyList<ValidationIssue> IssuesFor(string recordId, string fieldName)
        {
            return _byCell[(recordId ?? string.Empty, fieldName)].ToList();
        }

        public ValidationSeverity? HighestSeverityFor(string recordId, string fieldName)
        {
            return HighestSeverityAmong(IssuesFor(recordId, fieldName));
        }

        // Rolls up a nested/grouped issue (Core.Rules never names the owning Collection field
        // directly - only the element field, e.g. "weight") onto the record's Collection cell, so
        // the grid can show "something's wrong in here" before the row is even expanded. Requires
        // schema (passed to the constructor) to resolve which Collection field's ElementSchema the
        // issue's FieldName actually belongs to - without it, these two methods return empty/null.
        public IReadOnlyList<ValidationIssue> IssuesForCollectionField(string recordId, string collectionFieldName)
        {
            if (_byCollectionField == null)
                return System.Array.Empty<ValidationIssue>();

            return _byCollectionField[(recordId ?? string.Empty, collectionFieldName)].ToList();
        }

        public ValidationSeverity? HighestSeverityForCollectionField(string recordId, string collectionFieldName)
        {
            return HighestSeverityAmong(IssuesForCollectionField(recordId, collectionFieldName));
        }

        private static ValidationSeverity? HighestSeverityAmong(IReadOnlyList<ValidationIssue> issues)
        {
            if (issues.Count == 0)
                return null;

            return issues.Any(i => i.Severity == ValidationSeverity.Error) ? ValidationSeverity.Error : ValidationSeverity.Warning;
        }

        private static ILookup<(string, string), ValidationIssue> BuildCollectionFieldLookup(IReadOnlyList<ValidationIssue> issues, Schema schema)
        {
            var topLevelFieldNames = new HashSet<string>(schema.Fields.Select(f => f.Name));
            var collectionFields = schema.Fields.Where(f => f.Type == FieldType.Collection).ToList();

            var entries = new List<((string RecordId, string CollectionFieldName) Key, ValidationIssue Issue)>();
            foreach (var issue in issues)
            {
                var location = ValidationIssueLocator.Locate(issue);
                if (location.IsCollectionWide || location.ParentRecordId == null)
                    continue;

                // A field name matching a top-level schema field is a genuine top-level issue
                // (already served by _byCell), not a nested one masquerading in the same
                // plain-parent-id shape (SumEqualsRule + nestedField with no groupByField emits
                // exactly this ambiguous shape) - field-name disambiguation is what makes the
                // rollup work for that case, since the RecordId shape alone can't tell them apart.
                if (topLevelFieldNames.Contains(issue.FieldName))
                    continue;

                var owningField = collectionFields.FirstOrDefault(f =>
                    f.ElementSchema != null && f.ElementSchema.Fields.Any(ef => ef.Name == issue.FieldName));
                if (owningField == null)
                    continue;

                entries.Add(((location.ParentRecordId, owningField.Name), issue));
            }

            return entries.ToLookup(e => e.Key, e => e.Issue);
        }
    }
}
