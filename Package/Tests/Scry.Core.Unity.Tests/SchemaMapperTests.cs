using NUnit.Framework;
using Scry.Core;
using Scry.Core.Unity.Tests.Fixtures;

namespace Scry.Core.Unity.Tests
{
    public class SchemaMapperTests
    {
        [Test]
        public void InferSchema_MapsKnownFieldTypes()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestItemData));

            Assert.AreEqual(FieldType.String, schema.GetField("itemName").Type);
            Assert.AreEqual(FieldType.Numeric, schema.GetField("weight").Type);
            Assert.AreEqual(FieldType.Numeric, schema.GetField("dropChance").Type);
            Assert.AreEqual(FieldType.Boolean, schema.GetField("isUnique").Type);
            Assert.AreEqual(FieldType.Enum, schema.GetField("rarity").Type);
            Assert.AreEqual(FieldType.Reference, schema.GetField("referencedItem").Type);
        }

        [Test]
        public void InferSchema_MarksUnmappableFieldAsUnsupported_WithoutThrowing()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestItemData));

            var field = schema.GetField("unsupportedField");

            Assert.IsNotNull(field);
            Assert.AreEqual(FieldType.Unsupported, field.Type);
        }

        [Test]
        public void InferSchema_UsesTypeNameAsSchemaTypeName()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestItemData));

            Assert.AreEqual("TestItemData", schema.TypeName);
        }
    }
}
