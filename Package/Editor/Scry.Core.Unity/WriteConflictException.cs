using System;

namespace Scry.Core.Unity
{
    public sealed class WriteConflictException : Exception
    {
        public string RecordId { get; }

        public WriteConflictException(string recordId)
            : base($"Asset for record '{recordId}' changed since it was loaded. Reload before saving.")
        {
            RecordId = recordId;
        }
    }
}
