using Scry.Core;

namespace Scry.UI
{
    public readonly struct IssueLocation
    {
        public bool IsCollectionWide { get; }
        public string ParentRecordId { get; }
        public string NestedFieldGroupKey { get; }
        public int? ChildIndex { get; }

        public IssueLocation(bool isCollectionWide, string parentRecordId, string nestedFieldGroupKey, int? childIndex)
        {
            IsCollectionWide = isCollectionWide;
            ParentRecordId = parentRecordId;
            NestedFieldGroupKey = nestedFieldGroupKey;
            ChildIndex = childIndex;
        }
    }

    // Interprets the RecordId shapes ValidationIssue can carry. Scry.Core.Rules never formalizes
    // this as a type - it's purely a UI-side concern for deciding which row to highlight and
    // whether to expand a parent's Collection field first:
    //   null or "*"          - collection-wide, not tied to any row
    //   "{parentId}"          - a top-level row, or the parent of a nested-field issue
    //   "{parentId}/{key}"    - a nested-field issue grouped by a sub-field (SumEqualsRule + groupByField)
    //   "{parentId}#{index}"  - one specific entry inside a Collection field (NoDuplicateRule)
    public static class ValidationIssueLocator
    {
        private const string UngroupedKey = "*";

        public static IssueLocation Locate(ValidationIssue issue)
        {
            var recordId = issue.RecordId;

            if (string.IsNullOrEmpty(recordId) || recordId == UngroupedKey)
                return new IssueLocation(isCollectionWide: true, parentRecordId: null, nestedFieldGroupKey: null, childIndex: null);

            var groupSeparatorIndex = recordId.IndexOf('/');
            if (groupSeparatorIndex >= 0)
            {
                var parentId = recordId.Substring(0, groupSeparatorIndex);
                var groupKey = recordId.Substring(groupSeparatorIndex + 1);
                return new IssueLocation(isCollectionWide: false, parentRecordId: parentId, nestedFieldGroupKey: groupKey, childIndex: null);
            }

            var indexSeparatorIndex = recordId.IndexOf('#');
            if (indexSeparatorIndex >= 0)
            {
                var parentId = recordId.Substring(0, indexSeparatorIndex);
                var indexText = recordId.Substring(indexSeparatorIndex + 1);
                if (int.TryParse(indexText, out var index))
                    return new IssueLocation(isCollectionWide: false, parentRecordId: parentId, nestedFieldGroupKey: null, childIndex: index);

                return new IssueLocation(isCollectionWide: false, parentRecordId: recordId, nestedFieldGroupKey: null, childIndex: null);
            }

            return new IssueLocation(isCollectionWide: false, parentRecordId: recordId, nestedFieldGroupKey: null, childIndex: null);
        }
    }
}
