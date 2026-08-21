using NUnit.Framework;

namespace Scry.Core.Tests
{
    public class SchemaTests
    {
        [Test]
        public void GetField_ReturnsMatchingDescriptor()
        {
            var schema = new Schema("Item", new[]
            {
                new FieldDescriptor("weight", FieldType.Numeric),
                new FieldDescriptor("itemName", FieldType.String)
            });

            var field = schema.GetField("itemName");

            Assert.That(field, Is.Not.Null);
            Assert.That(field.Type, Is.EqualTo(FieldType.String));
        }

        [Test]
        public void GetField_ReturnsNullForUnknownField()
        {
            var schema = new Schema("Item", new[] { new FieldDescriptor("weight", FieldType.Numeric) });

            Assert.That(schema.GetField("doesNotExist"), Is.Null);
        }

        [Test]
        public void Fields_ExposesAllDescriptorsInOrder()
        {
            var schema = new Schema("Item", new[]
            {
                new FieldDescriptor("a", FieldType.Numeric),
                new FieldDescriptor("b", FieldType.String)
            });

            Assert.That(schema.Fields.Count, Is.EqualTo(2));
            Assert.That(schema.Fields[0].Name, Is.EqualTo("a"));
            Assert.That(schema.Fields[1].Name, Is.EqualTo("b"));
        }
    }
}
