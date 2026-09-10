using System.Collections.Generic;
using NUnit.Framework;
using Scry.Core;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Scry.UI.Tests
{
    public class CellBinderTests
    {
        [Test]
        public void CreateCell_Numeric_ReturnsFloatField()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);

            var cell = CellBinder.CreateCell(field);

            Assert.IsInstanceOf<FloatField>(cell);
        }

        [Test]
        public void CreateCell_String_ReturnsTextField()
        {
            var field = new FieldDescriptor("itemName", FieldType.String);

            Assert.IsInstanceOf<TextField>(CellBinder.CreateCell(field));
        }

        [Test]
        public void CreateCell_Boolean_ReturnsToggle()
        {
            var field = new FieldDescriptor("isUnique", FieldType.Boolean);

            Assert.IsInstanceOf<Toggle>(CellBinder.CreateCell(field));
        }

        [Test]
        public void CreateCell_Reference_ReturnsObjectField()
        {
            var field = new FieldDescriptor("referencedItem", FieldType.Reference);

            Assert.IsInstanceOf<ObjectField>(CellBinder.CreateCell(field));
        }

        [Test]
        public void CreateCell_Collection_ReturnsLabel()
        {
            var elementSchema = new Schema("Entry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var field = new FieldDescriptor("dropTable", FieldType.Collection, elementSchema);

            Assert.IsInstanceOf<Label>(CellBinder.CreateCell(field));
        }

        [Test]
        public void BindCell_Numeric_SetsInitialValueFromRecord()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);
            var cell = (FloatField)CellBinder.CreateCell(field);
            var record = new DataRecord("r1", new Dictionary<string, object> { ["weight"] = 5f });

            CellBinder.BindCell(cell, field, record, _ => { });

            Assert.AreEqual(5f, cell.value);
        }

        [Test]
        public void BindCell_Numeric_InvokesCallbackOnChange()
        {
            var field = new FieldDescriptor("weight", FieldType.Numeric);
            var cell = (FloatField)CellBinder.CreateCell(field);
            var record = new DataRecord("r1", new Dictionary<string, object> { ["weight"] = 5f });
            object receivedValue = null;

            CellBinder.BindCell(cell, field, record, v => receivedValue = v);
            cell.value = 42f;

            Assert.AreEqual(42f, receivedValue);
        }

        [Test]
        public void BindCell_Numeric_RebindingToADifferentRecord_DoesNotStackCallbacks()
        {
            // MultiColumnTreeView reuses the same VisualElement across virtualized rows, calling
            // BindCell again on every rebind - a naive RegisterValueChangedCallback would leak a
            // new handler each time, firing the edit callback multiple times per keystroke.
            var field = new FieldDescriptor("weight", FieldType.Numeric);
            var cell = (FloatField)CellBinder.CreateCell(field);
            var record1 = new DataRecord("r1", new Dictionary<string, object> { ["weight"] = 5f });
            var record2 = new DataRecord("r2", new Dictionary<string, object> { ["weight"] = 9f });
            var callCount = 0;

            CellBinder.BindCell(cell, field, record1, _ => callCount++);
            CellBinder.BindCell(cell, field, record2, _ => callCount++);
            cell.value = 42f;

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void BindCell_Collection_ShowsEntryCount()
        {
            var elementSchema = new Schema("Entry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var field = new FieldDescriptor("dropTable", FieldType.Collection, elementSchema);
            var cell = (Label)CellBinder.CreateCell(field);
            var entries = new List<DataRecord>
            {
                new DataRecord("r1#0", new Dictionary<string, object> { ["itemId"] = "sword" }),
                new DataRecord("r1#1", new Dictionary<string, object> { ["itemId"] = "shield" })
            };
            var record = new DataRecord("r1", new Dictionary<string, object> { ["dropTable"] = (IReadOnlyList<DataRecord>)entries });

            CellBinder.BindCell(cell, field, record, _ => { });

            Assert.AreEqual("2 entries", cell.text);
        }

        [Test]
        public void BindCell_Collection_EmptyList_ShowsZeroEntries()
        {
            var elementSchema = new Schema("Entry", new[] { new FieldDescriptor("itemId", FieldType.String) });
            var field = new FieldDescriptor("dropTable", FieldType.Collection, elementSchema);
            var cell = (Label)CellBinder.CreateCell(field);
            var record = new DataRecord("r1", new Dictionary<string, object> { ["dropTable"] = (IReadOnlyList<DataRecord>)new List<DataRecord>() });

            CellBinder.BindCell(cell, field, record, _ => { });

            Assert.AreEqual("0 entries", cell.text);
        }
    }
}
