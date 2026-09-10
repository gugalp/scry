// Package/Editor/Scry.UI/GridRow.cs
using Scry.Core;

namespace Scry.UI
{
    // One tree row's view model. IsTopLevel distinguishes an actual ScriptableObject record's
    // row from the detail row a Collection field expands into (Task 9) - a detail row's Record
    // is still its PARENT record (there's exactly one detail child per parent, hosting every
    // Collection field's own embedded sub-grid), not a nested entry's own record.
    public sealed class GridRow
    {
        public DataRecord Record { get; }
        public bool IsTopLevel { get; }

        public GridRow(DataRecord record, bool isTopLevel)
        {
            Record = record;
            IsTopLevel = isTopLevel;
        }
    }
}
