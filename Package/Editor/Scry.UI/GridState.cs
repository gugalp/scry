using System;
using System.Collections.Generic;
using System.Linq;
using Scry.Core;
using Scry.Core.Unity.Config;

namespace Scry.UI
{
    // Holds one open DataGridWindow tab's live data: the scanned DataCollection and the
    // ValidationIssues it currently produces. ReplaceRecord is how an edit propagates - it swaps
    // one record in the collection and re-runs validation, so the grid always reflects the asset
    // state actually on disk after a write.
    public sealed class GridState
    {
        public Type ScriptableObjectType { get; }
        public IReadOnlyList<RuleConfig> Rules { get; }
        public DataCollection Collection { get; private set; }
        public ValidationIssueIndex Issues { get; private set; }

        public GridState(Type scriptableObjectType, DataCollection collection, IReadOnlyList<RuleConfig> rules)
        {
            ScriptableObjectType = scriptableObjectType;
            Rules = rules;
            Collection = collection;
            Issues = new ValidationIssueIndex(ValidationRunner.Run(collection, rules));
        }

        public void ReplaceRecord(DataRecord updated)
        {
            var records = Collection.Records.Select(r => r.Id == updated.Id ? updated : r).ToList();
            Collection = new DataCollection(Collection.Schema, records);
            Issues = new ValidationIssueIndex(ValidationRunner.Run(Collection, Rules));
        }
    }
}
