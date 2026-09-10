using NUnit.Framework;
using Scry.Core;

namespace Scry.UI.Tests
{
    public class ValidationIssueLocatorTests
    {
        [Test]
        public void Locate_NullRecordId_IsCollectionWide()
        {
            var issue = new ValidationIssue(null, "weight", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.IsTrue(location.IsCollectionWide);
            Assert.IsNull(location.ParentRecordId);
        }

        [Test]
        public void Locate_UngroupedSentinel_IsCollectionWide()
        {
            var issue = new ValidationIssue("*", "weight", "message");

            Assert.IsTrue(ValidationIssueLocator.Locate(issue).IsCollectionWide);
        }

        [Test]
        public void Locate_PlainGuid_IsParentRecordOnly()
        {
            var issue = new ValidationIssue("abc123", "weight", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.IsFalse(location.IsCollectionWide);
            Assert.AreEqual("abc123", location.ParentRecordId);
            Assert.IsNull(location.NestedFieldGroupKey);
            Assert.IsNull(location.ChildIndex);
        }

        [Test]
        public void Locate_GroupedNestedId_ParsesParentAndGroupKey()
        {
            var issue = new ValidationIssue("abc123/goblin", "weight", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.IsFalse(location.IsCollectionWide);
            Assert.AreEqual("abc123", location.ParentRecordId);
            Assert.AreEqual("goblin", location.NestedFieldGroupKey);
            Assert.IsNull(location.ChildIndex);
        }

        [Test]
        public void Locate_ChildEntryId_ParsesParentAndIndex()
        {
            var issue = new ValidationIssue("abc123#2", "itemId", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.IsFalse(location.IsCollectionWide);
            Assert.AreEqual("abc123", location.ParentRecordId);
            Assert.AreEqual(2, location.ChildIndex);
            Assert.IsNull(location.NestedFieldGroupKey);
        }

        [Test]
        public void Locate_MalformedIndexSuffix_FallsBackToTreatingWholeIdAsParent()
        {
            var issue = new ValidationIssue("abc123#notanumber", "itemId", "message");

            var location = ValidationIssueLocator.Locate(issue);

            Assert.AreEqual("abc123#notanumber", location.ParentRecordId);
            Assert.IsNull(location.ChildIndex);
        }
    }
}
