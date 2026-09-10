using System;
using System.Collections.Generic;
using Scry.Core;

namespace Scry.UI
{
    public enum ColumnFilterMode
    {
        Contains,
        Range,
        EnumValues,
        BooleanState
    }

    public sealed class ColumnFilter
    {
        public string FieldName { get; }
        public ColumnFilterMode Mode { get; }
        public string TextContains { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public HashSet<int> AllowedEnumValues { get; set; }
        public bool? BooleanValue { get; set; }

        public ColumnFilter(string fieldName, ColumnFilterMode mode)
        {
            FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
            Mode = mode;
        }
    }

    public static class RowFilter
    {
        public static bool Matches(DataRecord record, Schema schema, string searchText, IReadOnlyList<ColumnFilter> columnFilters)
        {
            if (!MatchesSearchText(record, schema, searchText))
                return false;

            if (columnFilters == null)
                return true;

            foreach (var filter in columnFilters)
            {
                if (!MatchesColumnFilter(record, filter))
                    return false;
            }

            return true;
        }

        private static bool MatchesSearchText(DataRecord record, Schema schema, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return true;

            foreach (var field in schema.Fields)
            {
                if (field.Type == FieldType.Collection)
                    continue;

                var value = record.GetValue(field.Name);
                if (value != null && value.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool MatchesColumnFilter(DataRecord record, ColumnFilter filter)
        {
            var value = record.GetValue(filter.FieldName);

            switch (filter.Mode)
            {
                case ColumnFilterMode.Contains:
                    return string.IsNullOrEmpty(filter.TextContains)
                        || (value != null && value.ToString().IndexOf(filter.TextContains, StringComparison.OrdinalIgnoreCase) >= 0);
                case ColumnFilterMode.Range:
                    var numeric = value == null ? 0 : Convert.ToDouble(value);
                    if (filter.Min.HasValue && numeric < filter.Min.Value)
                        return false;
                    if (filter.Max.HasValue && numeric > filter.Max.Value)
                        return false;
                    return true;
                case ColumnFilterMode.EnumValues:
                    if (filter.AllowedEnumValues == null || filter.AllowedEnumValues.Count == 0)
                        return true;
                    return filter.AllowedEnumValues.Contains(Convert.ToInt32(value ?? 0));
                case ColumnFilterMode.BooleanState:
                    if (!filter.BooleanValue.HasValue)
                        return true;
                    return (value is bool b && b) == filter.BooleanValue.Value;
                default:
                    return true;
            }
        }
    }
}
