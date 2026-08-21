using System.Collections.Generic;
using NUnit.Framework;

namespace Scry.Core.Tests
{
    public class DataCollectionTests
    {
        [Test]
        public void Records_ExposesValuesByFieldName()
        {
            var schema = new Schema("Item", new[] { new FieldDescriptor("weight", FieldType.Numeric) });
            var record = new DataRecord("asset-1", new Dictionary<string, object> { ["weight"] = 5 });
            var collection = new DataCollection(schema, new[] { record });

            Assert.That(collection.Records.Count, Is.EqualTo(1));
            Assert.That(collection.Records[0].GetValue("weight"), Is.EqualTo(5));
        }

        [Test]
        public void GetValue_ReturnsNullForMissingField()
        {
            var record = new DataRecord("asset-1", new Dictionary<string, object>());

            Assert.That(record.GetValue("doesNotExist"), Is.Null);
        }

        [Test]
        public void Fingerprint_DefaultsToNull()
        {
            var record = new DataRecord("asset-1", new Dictionary<string, object>());

            Assert.That(record.Fingerprint, Is.Null);
        }

        [Test]
        public void Fingerprint_CanBeSetExplicitly()
        {
            var record = new DataRecord("asset-1", new Dictionary<string, object>(), fingerprint: "hash-abc");

            Assert.That(record.Fingerprint, Is.EqualTo("hash-abc"));
        }

        [Test]
        public void Constructor_ThrowsOnEmptyId()
        {
            Assert.Throws<System.ArgumentException>(() => new DataRecord("", new Dictionary<string, object>()));
        }
    }
}
