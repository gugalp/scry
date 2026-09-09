using System;
using NUnit.Framework;

namespace Scry.Core.Tests
{
    public class FieldDescriptorTests
    {
        [Test]
        public void Constructor_SetsNameAndType()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);

            Assert.That(field.Name, Is.EqualTo("weight"));
            Assert.That(field.Type, Is.EqualTo(FieldType.Numeric));
        }

        [Test]
        public void IsSupported_FalseForUnsupportedType()
        {
            var field = new FieldDescriptor("mystery", FieldType.Unsupported);

            Assert.That(field.IsSupported, Is.False);
        }

        [Test]
        public void IsSupported_TrueForKnownType()
        {
            var field = new FieldDescriptor("itemName", FieldType.String);

            Assert.That(field.IsSupported, Is.True);
        }

        [Test]
        public void Constructor_ThrowsOnEmptyName()
        {
            Assert.Throws<ArgumentException>(() => new FieldDescriptor("", FieldType.String));
        }

        [Test]
        public void Constructor_SetsElementSchema_ForCollectionType()
        {
            var elementSchema = new Schema("DropEntry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var field = new FieldDescriptor("dropTable", FieldType.Collection, elementSchema);

            Assert.That(field.Type, Is.EqualTo(FieldType.Collection));
            Assert.That(field.ElementSchema, Is.SameAs(elementSchema));
        }

        [Test]
        public void Constructor_ElementSchemaDefaultsToNull()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);

            Assert.That(field.ElementSchema, Is.Null);
        }
    }
}
