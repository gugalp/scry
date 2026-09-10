using System.Collections.Generic;
using NUnit.Framework;
using Scry.Core;

namespace Scry.UI.Tests
{
    public class RowFilterTests
    {
        private static readonly Schema TestSchema = new Schema("Item", new[]
        {
            new FieldDescriptor("itemName", FieldType.String),
            new FieldDescriptor("weight", FieldType.Numeric),
            new FieldDescriptor("rarity", FieldType.Enum),
            new FieldDescriptor("isUnique", FieldType.Boolean)
        });

        private static DataRecord BuildRecord(string itemName, double weight, int rarity, bool isUnique)
        {
            return new DataRecord("r1", new Dictionary<string, object>
            {
                ["itemName"] = itemName,
                ["weight"] = weight,
                ["rarity"] = rarity,
                ["isUnique"] = isUnique
            });
        }

        [Test]
        public void Matches_NoSearchTextNoFilters_ReturnsTrue()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);

            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, null));
        }

        [Test]
        public void Matches_SearchText_MatchesAnyFieldCaseInsensitively()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);

            Assert.IsTrue(RowFilter.Matches(record, TestSchema, "rusty", null));
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, "shield", null));
        }

        [Test]
        public void Matches_ContainsColumnFilter_FiltersOnThatField()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);
            var filters = new[] { new ColumnFilter("itemName", ColumnFilterMode.Contains) { TextContains = "sword" } };

            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, filters));

            filters = new[] { new ColumnFilter("itemName", ColumnFilterMode.Contains) { TextContains = "shield" } };
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, null, filters));
        }

        [Test]
        public void Matches_RangeColumnFilter_RespectsMinAndMax()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);

            var withinRange = new[] { new ColumnFilter("weight", ColumnFilterMode.Range) { Min = 1, Max = 10 } };
            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, withinRange));

            var outsideRange = new[] { new ColumnFilter("weight", ColumnFilterMode.Range) { Min = 6 } };
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, null, outsideRange));
        }

        [Test]
        public void Matches_EnumValuesColumnFilter_RestrictsToAllowedSet()
        {
            var record = BuildRecord("Rusty Sword", 5, 2, false);

            var allowed = new[] { new ColumnFilter("rarity", ColumnFilterMode.EnumValues) { AllowedEnumValues = new HashSet<int> { 2, 3 } } };
            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, allowed));

            var disallowed = new[] { new ColumnFilter("rarity", ColumnFilterMode.EnumValues) { AllowedEnumValues = new HashSet<int> { 0, 1 } } };
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, null, disallowed));
        }

        [Test]
        public void Matches_BooleanStateColumnFilter_RestrictsToThatState()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, true);

            var wantTrue = new[] { new ColumnFilter("isUnique", ColumnFilterMode.BooleanState) { BooleanValue = true } };
            Assert.IsTrue(RowFilter.Matches(record, TestSchema, null, wantTrue));

            var wantFalse = new[] { new ColumnFilter("isUnique", ColumnFilterMode.BooleanState) { BooleanValue = false } };
            Assert.IsFalse(RowFilter.Matches(record, TestSchema, null, wantFalse));
        }

        [Test]
        public void Matches_SearchTextAndColumnFilter_BothMustPass()
        {
            var record = BuildRecord("Rusty Sword", 5, 0, false);
            var filters = new[] { new ColumnFilter("weight", ColumnFilterMode.Range) { Min = 10 } };

            Assert.IsFalse(RowFilter.Matches(record, TestSchema, "rusty", filters));
        }
    }
}
