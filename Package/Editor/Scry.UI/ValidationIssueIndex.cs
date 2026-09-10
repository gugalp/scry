using System.Collections.Generic;
using System.Linq;
using Scry.Core;

namespace Scry.UI
{
    public sealed class ValidationIssueIndex
    {
        private readonly ILookup<(string RecordId, string FieldName), ValidationIssue> _byCell;

        public IReadOnlyList<ValidationIssue> AllIssues { get; }

        public ValidationIssueIndex(IReadOnlyList<ValidationIssue> issues)
        {
            AllIssues = issues;
            _byCell = issues.ToLookup(i => (ValidationIssueLocator.Locate(i).ParentRecordId ?? string.Empty, i.FieldName));
        }

        public IReadOnlyList<ValidationIssue> IssuesFor(string recordId, string fieldName)
        {
            return _byCell[(recordId ?? string.Empty, fieldName)].ToList();
        }

        public ValidationSeverity? HighestSeverityFor(string recordId, string fieldName)
        {
            var issues = IssuesFor(recordId, fieldName);
            if (issues.Count == 0)
                return null;

            return issues.Any(i => i.Severity == ValidationSeverity.Error) ? ValidationSeverity.Error : ValidationSeverity.Warning;
        }
    }
}
