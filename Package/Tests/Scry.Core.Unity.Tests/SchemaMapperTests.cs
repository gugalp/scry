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

        [Test]
        public void InferSchema_IncludesPrivateSerializeFieldInheritedFromBaseClass()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestDerivedItemData));

            var baseField = schema.GetField("baseId");
            Assert.IsNotNull(baseField, "Private [SerializeField] field declared on a base class should not be dropped.");
            Assert.AreEqual(FieldType.Numeric, baseField.Type);

            // Sanity-check the derived type's own fields are still present alongside the inherited one.
            Assert.AreEqual(FieldType.String, schema.GetField("derivedName").Type);
            Assert.AreEqual(FieldType.Numeric, schema.GetField("derivedWeight").Type);
        }

        [Test]
        public void InferSchema_MapsListOfSerializableClass_AsCollectionWithElementSchema()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestMonsterData));

            var field = schema.GetField("dropTable");

            Assert.AreEqual(FieldType.Collection, field.Type);
            Assert.IsNotNull(field.ElementSchema);
            Assert.AreEqual(FieldType.String, field.ElementSchema.GetField("itemId").Type);
            Assert.AreEqual(FieldType.Numeric, field.ElementSchema.GetField("weight").Type);
        }

        [Test]
        public void InferSchema_DoesNotAllowNestedCollectionsWithinAnElementSchema()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestNestedListData));

            var field = schema.GetField("outer");

            Assert.AreEqual(FieldType.Collection, field.Type);
            Assert.AreEqual(FieldType.Unsupported, field.ElementSchema.GetField("inner").Type);
        }

        [Test]
        public void InferSchema_RejectsDirectlyNestedListType_AsUnsupported()
        {
            var schema = SchemaMapper.InferSchema(typeof(TestDoublyNestedListData));

            var field = schema.GetField("doublyNested");

            Assert.AreEqual(FieldType.Unsupported, field.Type, "A field directly typed as List<List<T>> should be Unsupported, not Collection.");
        }

        [Test]
        public void InferSchema_RejectsListOfString_AsUnsupported_NotCollection()
        {
            // System.String is a [Serializable] class in the BCL, so a naive "is it a plain
            // serializable class" check would misclassify List<string>/string[] as a Collection
            // field whose element schema has zero fields (String has no [SerializeField]
            // instance fields), making the string values unreadable/uneditable.
            var schema = SchemaMapper.InferSchema(typeof(TestStringListData));

            var field = schema.GetField("tags");

            Assert.AreEqual(FieldType.Unsupported, field.Type);
        }
    }
}
