// Package/Tests/Scry.UI.Tests/ValidationIssueIndexTests.cs
using System.Collections.Generic;
using NUnit.Framework;
using Scry.Core;

namespace Scry.UI.Tests
{
    public class ValidationIssueIndexTests
    {
        [Test]
        public void IssuesFor_ReturnsIssuesMatchingRecordAndField()
        {
            var issues = new List<ValidationIssue>
            {
                new ValidationIssue("r1", "weight", "too heavy"),
                new ValidationIssue("r1", "itemName", "duplicate name"),
                new ValidationIssue("r2", "weight", "too heavy")
            };
            var index = new ValidationIssueIndex(issues);

            var result = index.IssuesFor("r1", "weight");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("too heavy", result[0].Message);
        }

        [Test]
        public void IssuesFor_NoMatch_ReturnsEmpty()
        {
            var index = new ValidationIssueIndex(new List<ValidationIssue>());

            Assert.IsEmpty(index.IssuesFor("r1", "weight"));
        }

        [Test]
        public void HighestSeverityFor_NoIssues_ReturnsNull()
        {
            var index = new ValidationIssueIndex(new List<ValidationIssue>());

            Assert.IsNull(index.HighestSeverityFor("r1", "weight"));
        }

        [Test]
        public void HighestSeverityFor_PrefersErrorOverWarning()
        {
            var issues = new List<ValidationIssue>
            {
                new ValidationIssue("r1", "weight", "warn", ValidationSeverity.Warning),
                new ValidationIssue("r1", "weight", "err", ValidationSeverity.Error)
            };
            var index = new ValidationIssueIndex(issues);

            Assert.AreEqual(ValidationSeverity.Error, index.HighestSeverityFor("r1", "weight"));
        }

        [Test]
        public void IssuesFor_NestedChildEntryId_MatchesByParsedParentId()
        {
            // NoDuplicateRule emits the CHILD's own id ("r1#1"), not the parent's - IssuesFor
            // should still be queryable by the parent record id the UI actually has on hand for
            // a top-level row's decoration, via the locator's parsed ParentRecordId.
            var issues = new List<ValidationIssue> { new ValidationIssue("r1#1", "itemId", "duplicate") };
            var index = new ValidationIssueIndex(issues);

            Assert.AreEqual(1, index.IssuesFor("r1", "itemId").Count);
        }

        [Test]
        public void AllIssues_ReturnsEveryIssuePassedIn()
        {
            var issues = new List<ValidationIssue>
            {
                new ValidationIssue("r1", "weight", "a"),
                new ValidationIssue("r2", "weight", "b")
            };

            var index = new ValidationIssueIndex(issues);

            Assert.AreEqual(2, index.AllIssues.Count);
        }

        private static Schema BuildMonsterSchema()
        {
            var elementSchema = new Schema("DropEntry", new[]
            {
                new FieldDescriptor("itemId", FieldType.String),
                new FieldDescriptor("weight", FieldType.Numeric)
            });
            return new Schema("Monster", new[]
            {
                new FieldDescriptor("monsterName", FieldType.String),
                new FieldDescriptor("dropTable", FieldType.Collection, elementSchema)
            });
        }

        [Test]
        public void HighestSeverityForCollectionField_RollsUpAmbiguousUngroupedNestedIssue_WhenSchemaProvided()
        {
            // SumEqualsRule with nestedField but no groupByField emits the PARENT's plain id - the
            // same shape a genuine top-level field issue would have. The rollup must disambiguate
            // by field name (not RecordId shape) since "weight" isn't a top-level Monster field.
            var issues = new List<ValidationIssue> { new ValidationIssue("r1", "weight", "sum mismatch") };

            var index = new ValidationIssueIndex(issues, BuildMonsterSchema());

            Assert.AreEqual(ValidationSeverity.Error, index.HighestSeverityForCollectionField("r1", "dropTable"));
        }

        [Test]
        public void IssuesForCollectionField_RollsUpGroupedNestedIssue()
        {
            var issues = new List<ValidationIssue> { new ValidationIssue("r1/goblin", "weight", "sum mismatch") };

            var index = new ValidationIssueIndex(issues, BuildMonsterSchema());

            Assert.AreEqual(1, index.IssuesForCollectionField("r1", "dropTable").Count);
        }

        [Test]
        public void IssuesForCollectionField_RollsUpPerEntryNestedIssue()
        {
            var issues = new List<ValidationIssue> { new ValidationIssue("r1#1", "itemId", "duplicate") };

            var index = new ValidationIssueIndex(issues, BuildMonsterSchema());

            Assert.AreEqual(1, index.IssuesForCollectionField("r1", "dropTable").Count);
        }

        [Test]
        public void IssuesForCollectionField_DoesNotIncludeTopLevelFieldIssues()
        {
            var issues = new List<ValidationIssue> { new ValidationIssue("r1", "monsterName", "duplicate name") };

            var index = new ValidationIssueIndex(issues, BuildMonsterSchema());

            Assert.IsEmpty(index.IssuesForCollectionField("r1", "dropTable"));
            Assert.AreEqual(1, index.IssuesFor("r1", "monsterName").Count);
        }

        [Test]
        public void IssuesForCollectionField_IgnoresFieldNameNotBelongingToAnyCollectionField()
        {
            var issues = new List<ValidationIssue> { new ValidationIssue("r1", "somethingElse", "orphan issue") };

            var index = new ValidationIssueIndex(issues, BuildMonsterSchema());

            Assert.IsEmpty(index.IssuesForCollectionField("r1", "dropTable"));
        }

        [Test]
        public void HighestSeverityForCollectionField_ReturnsNull_WhenNoSchemaProvided()
        {
            var issues = new List<ValidationIssue> { new ValidationIssue("r1", "weight", "sum mismatch") };
            var index = new ValidationIssueIndex(issues);

            Assert.IsNull(index.HighestSeverityForCollectionField("r1", "dropTable"));
            Assert.IsEmpty(index.IssuesForCollectionField("r1", "dropTable"));
        }
    }
}
