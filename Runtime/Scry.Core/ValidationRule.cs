using System.Collections.Generic;

namespace Scry.Core
{
    public abstract class ValidationRule
    {
        public abstract IEnumerable<ValidationIssue> Evaluate(DataCollection collection);
    }
}
