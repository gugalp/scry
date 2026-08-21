namespace Scry.Core
{
    public sealed class ValidationIssue
    {
        public string RecordId { get; }
        public string FieldName { get; }
        public string Message { get; }
        public ValidationSeverity Severity { get; }

        public ValidationIssue(string recordId, string fieldName, string message, ValidationSeverity severity = ValidationSeverity.Error)
        {
            RecordId = recordId;
            FieldName = fieldName;
            Message = message;
            Severity = severity;
        }
    }
}
