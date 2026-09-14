using System;
using System.Collections.Generic;
using System.Linq;
using Scry.Core.Unity;
using Scry.Core.Unity.Config;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scry.UI
{
    public sealed class DataGridWindow : EditorWindow
    {
        [MenuItem("Scry/Data Editor")]
        public static void Open()
        {
            var window = GetWindow<DataGridWindow>();
            window.titleContent = new GUIContent("Scry Data Editor");
        }

        private readonly ScriptableObjectRepository _repository = new ScriptableObjectRepository();
        private readonly Dictionary<string, int> _idsByRecordId = new Dictionary<string, int>();
        private int _nextId = 1;

        private ScryConfig _config;
        private VisualElement _tabStrip;
        private VisualElement _content;
        private GridState _activeState;

        private readonly HashSet<string> _selectedRecordIds = new HashSet<string>();
        private string _searchText = string.Empty;

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            _config = FindScryConfig();
            if (_config == null)
            {
                rootVisualElement.Add(new Label("No ScryConfig asset found in the project. Create one via Assets > Create > Scry > Config."));
                return;
            }

            if (_config.TrackedCollections.Count == 0)
            {
                rootVisualElement.Add(new Label("ScryConfig has no tracked collections. Add one via its Inspector."));
                return;
            }

            _tabStrip = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _content = new VisualElement { style = { flexGrow = 1 } };
            rootVisualElement.Add(_tabStrip);
            rootVisualElement.Add(_content);

            foreach (var tracked in _config.TrackedCollections)
            {
                var capturedTracked = tracked;
                var button = new Button(() => SelectTab(capturedTracked)) { text = ShortTypeName(capturedTracked.TypeName) };
                _tabStrip.Add(button);
            }

            SelectTab(_config.TrackedCollections[0]);
        }

        private static ScryConfig FindScryConfig()
        {
            var guids = AssetDatabase.FindAssets("t:ScryConfig");
            if (guids.Length == 0)
                return null;

            if (guids.Length > 1)
                Debug.LogWarning($"Scry: multiple ScryConfig assets found; using the first one ({AssetDatabase.GUIDToAssetPath(guids[0])}).");

            return AssetDatabase.LoadAssetAtPath<ScryConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static string ShortTypeName(string assemblyQualifiedName)
        {
            var commaIndex = assemblyQualifiedName.IndexOf(',');
            var fullName = commaIndex >= 0 ? assemblyQualifiedName.Substring(0, commaIndex) : assemblyQualifiedName;
            var dotIndex = fullName.LastIndexOf('.');
            return dotIndex >= 0 ? fullName.Substring(dotIndex + 1) : fullName;
        }

        private void SelectTab(TrackedCollection tracked)
        {
            var type = Type.GetType(tracked.TypeName);
            _content.Clear();
            _selectedRecordIds.Clear();
            _searchText = string.Empty;

            if (type == null)
            {
                _content.Add(new Label($"Could not resolve type '{tracked.TypeName}'."));
                return;
            }

            var collection = _repository.Scan(type);
            _activeState = new GridState(type, collection, tracked.Rules);

            var grid = BuildGrid(_activeState);
            var treeView = (MultiColumnTreeView)grid;

            _content.Add(BuildSearchBox(_activeState, treeView));
            _content.Add(BuildToolbar(_activeState, treeView));
            _content.Add(grid);
            _content.Add(BuildIssuesPanel(_activeState, treeView));
        }

        private int GetOrCreateId(string recordId)
        {
            if (_idsByRecordId.TryGetValue(recordId, out var id))
                return id;

            id = _nextId++;
            _idsByRecordId[recordId] = id;
            return id;
        }

        private sealed class TreeViewHolder
        {
            public MultiColumnTreeView TreeView;
        }

        private VisualElement BuildGrid(GridState state)
        {
            var holder = new TreeViewHolder();
            var columns = BuildColumns(state, holder);
            var treeView = new MultiColumnTreeView(columns) { style = { flexGrow = 1 }, virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight };
            holder.TreeView = treeView;

            RefreshTreeItems(treeView, state);

            return treeView;
        }

        private Columns BuildColumns(GridState state, TreeViewHolder holder)
        {
            var columns = new Columns();

            var selectionColumn = new Column
            {
                name = "__selected",
                title = string.Empty,
                width = 24,
                makeCell = () => new Toggle(),
                bindCell = (cell, rowIndex) =>
                {
                    var row = holder.TreeView.GetItemDataForIndex<GridRow>(rowIndex);
                    var toggle = (Toggle)cell;
                    if (!row.IsTopLevel)
                    {
                        toggle.style.display = DisplayStyle.None;
                        return;
                    }
                    toggle.style.display = DisplayStyle.Flex;
                    toggle.SetValueWithoutNotify(_selectedRecordIds.Contains(row.Record.Id));

                    if (toggle.userData is EventCallback<ChangeEvent<bool>> previousCallback)
                        toggle.UnregisterValueChangedCallback(previousCallback);

                    EventCallback<ChangeEvent<bool>> callback = evt =>
                    {
                        if (evt.newValue)
                            _selectedRecordIds.Add(row.Record.Id);
                        else
                            _selectedRecordIds.Remove(row.Record.Id);
                    };
                    toggle.userData = callback;
                    toggle.RegisterValueChangedCallback(callback);
                }
            };
            columns.Add(selectionColumn);

            var fields = new List<Scry.Core.FieldDescriptor>();
            foreach (var field in state.Collection.Schema.Fields)
            {
                if (field.IsSupported)
                    fields.Add(field);
            }

            for (var columnIndex = 0; columnIndex < fields.Count; columnIndex++)
            {
                var field = fields[columnIndex];
                var isDetailHostColumn = columnIndex == 0;

                columns.Add(new Column
                {
                    name = field.Name,
                    title = field.Name,
                    makeCell = () => new VisualElement(),
                    bindCell = (container, rowIndex) =>
                    {
                        container.Clear();
                        var row = holder.TreeView.GetItemDataForIndex<GridRow>(rowIndex);

                        if (!row.IsTopLevel)
                        {
                            if (isDetailHostColumn)
                                container.Add(BuildDetailPane(state, holder.TreeView, row.Record));
                            return;
                        }

                        var cell = CellBinder.CreateCell(field);
                        CellBinder.BindCell(cell, field, row.Record, newValue => OnCellEdited(state, holder.TreeView, row.Record, field.Name, newValue));
                        container.Add(cell);

                        // A Collection field's own issues are never keyed by its own field name
                        // (Core.Rules names the nested element field, e.g. "weight", not the
                        // owning Collection field, e.g. "dropTable") - HighestSeverityForCollectionField
                        // rolls those up so the cell can show "something's wrong in here" even
                        // before the row is expanded.
                        var severity = field.Type == Scry.Core.FieldType.Collection
                            ? state.Issues.HighestSeverityForCollectionField(row.Record.Id, field.Name)
                            : state.Issues.HighestSeverityFor(row.Record.Id, field.Name);
                        if (severity.HasValue)
                        {
                            var issues = field.Type == Scry.Core.FieldType.Collection
                                ? state.Issues.IssuesForCollectionField(row.Record.Id, field.Name)
                                : state.Issues.IssuesFor(row.Record.Id, field.Name);
                            var icon = new Label(severity.Value == Scry.Core.ValidationSeverity.Error ? "!" : "?")
                            {
                                tooltip = string.Join("\n", issues.Select(i => i.Message)),
                                style =
                                {
                                    color = severity.Value == Scry.Core.ValidationSeverity.Error ? Color.red : Color.yellow,
                                    unityFontStyleAndWeight = FontStyle.Bold
                                }
                            };
                            container.Add(icon);
                        }
                    }
                });
            }

            return columns;
        }

        private void OnCellEdited(GridState state, MultiColumnTreeView treeView, Scry.Core.DataRecord record, string fieldName, object newValue)
        {
            try
            {
                var updated = EditGateway.ApplyEdit(_repository, record, fieldName, newValue, state.ScriptableObjectType, $"Edit {fieldName}");
                state.ReplaceRecord(updated);
                // Every row's DataRecord (including its Fingerprint) must be refreshed after a write,
                // otherwise a second edit to the same row would be checked against a now-stale
                // fingerprint and spuriously throw WriteConflictException.
                RefreshTreeItemsInPlace(treeView, state);
            }
            catch (Scry.Core.Unity.WriteConflictException)
            {
                PromptReloadOnConflict(state, treeView, record.Id);
            }
        }

        private VisualElement BuildIssuesPanel(GridState state, MultiColumnTreeView treeView)
        {
            var foldout = new Foldout { text = $"Issues ({state.Issues.AllIssues.Count})", value = false };

            foreach (var issue in state.Issues.AllIssues)
            {
                var location = ValidationIssueLocator.Locate(issue);
                var row = new Button(() => JumpToIssue(state, treeView, location))
                {
                    text = $"[{issue.Severity}] {issue.FieldName}: {issue.Message}"
                };
                foldout.Add(row);
            }

            return foldout;
        }

        private void JumpToIssue(GridState state, MultiColumnTreeView treeView, IssueLocation location)
        {
            if (location.IsCollectionWide || location.ParentRecordId == null)
                return;

            var id = GetOrCreateId(location.ParentRecordId);
            treeView.SetSelectionById(id);
            treeView.ScrollToItemById(id);

            if (location.ChildIndex.HasValue || location.NestedFieldGroupKey != null)
            {
                treeView.ExpandItem(id);
            }
        }

        private void PromptReloadOnConflict(GridState state, MultiColumnTreeView treeView, string recordId)
        {
            if (!EditorUtility.DisplayDialog("Scry", $"The asset for '{recordId}' changed outside this window since it was loaded. Reload the collection to see the latest data?", "Reload", "Cancel"))
                return;

            var refreshed = _repository.Scan(state.ScriptableObjectType);
            var refreshedRecord = refreshed.Records.FirstOrDefault(r => r.Id == recordId);
            if (refreshedRecord != null)
                state.ReplaceRecord(refreshedRecord);
            RefreshTreeItemsInPlace(treeView, state);
        }

        // Rebuild() recreates every visible cell's VisualElement, which destroys keyboard focus,
        // scroll position, and row-expansion state - use it only when the set of visible rows
        // itself changes (search filtering). RefreshTreeItemsInPlace uses the lighter
        // RefreshItems() for every edit path, since none of them change which top-level records
        // are visible or how many there are - only record content changes.
        private void RefreshTreeItems(MultiColumnTreeView treeView, GridState state)
        {
            treeView.SetRootItems(BuildVisibleTreeItems(state));
            treeView.Rebuild();
        }

        private void RefreshTreeItemsInPlace(MultiColumnTreeView treeView, GridState state)
        {
            treeView.SetRootItems(BuildVisibleTreeItems(state));
            treeView.RefreshItems();
        }

        private List<TreeViewItemData<GridRow>> BuildVisibleTreeItems(GridState state)
        {
            var items = new List<TreeViewItemData<GridRow>>();
            foreach (var record in state.Collection.Records)
            {
                if (!RowFilter.Matches(record, state.Collection.Schema, _searchText, null))
                    continue;

                items.Add(BuildTreeItem(record, state.Collection.Schema));
            }

            return items;
        }

        private VisualElement BuildToolbar(GridState state, MultiColumnTreeView treeView)
        {
            var editableFieldNames = GetEditableFieldNames(state.Collection.Schema);
            if (editableFieldNames.Count == 0)
                return new VisualElement();

            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            var fieldNameDropdown = new PopupField<string>(editableFieldNames, 0);
            var valueField = new TextField { style = { minWidth = 80 } };
            var applyButton = new Button(() =>
            {
                var selected = GetSelectedRecords(state);
                if (selected.Count == 0)
                    return;

                var fieldDescriptor = state.Collection.Schema.GetField(fieldNameDropdown.value);
                var parsedValue = ParseValueForField(fieldDescriptor, valueField.value);

                try
                {
                    BulkEditor.ApplyToSelected(_repository, selected, fieldNameDropdown.value, parsedValue, state.ScriptableObjectType, updated => state.ReplaceRecord(updated));
                    RefreshTreeItemsInPlace(treeView, state);
                }
                catch (Scry.Core.Unity.WriteConflictException conflict)
                {
                    PromptReloadOnConflict(state, treeView, conflict.RecordId);
                }
            })
            { text = "Apply to selected" };

            toolbar.Add(fieldNameDropdown);
            toolbar.Add(valueField);
            toolbar.Add(applyButton);

            return toolbar;
        }

        private static List<string> GetEditableFieldNames(Scry.Core.Schema schema)
        {
            var names = new List<string>();
            foreach (var field in schema.Fields)
            {
                if (field.IsSupported && field.Type != Scry.Core.FieldType.Collection && field.Type != Scry.Core.FieldType.Reference)
                    names.Add(field.Name);
            }
            return names;
        }

        private static object ParseValueForField(Scry.Core.FieldDescriptor field, string rawText)
        {
            switch (field.Type)
            {
                case Scry.Core.FieldType.Numeric:
                    return float.TryParse(rawText, out var f) ? f : 0f;
                case Scry.Core.FieldType.Boolean:
                    return bool.TryParse(rawText, out var b) && b;
                case Scry.Core.FieldType.Enum:
                    return int.TryParse(rawText, out var i) ? i : 0;
                default:
                    return rawText;
            }
        }

        private List<Scry.Core.DataRecord> GetSelectedRecords(GridState state)
        {
            var selected = new List<Scry.Core.DataRecord>();
            foreach (var record in state.Collection.Records)
            {
                if (_selectedRecordIds.Contains(record.Id))
                    selected.Add(record);
            }
            return selected;
        }

        private VisualElement BuildSearchBox(GridState state, MultiColumnTreeView treeView)
        {
            var searchField = new ToolbarSearchField();
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue;
                RefreshTreeItems(treeView, state);
            });
            return searchField;
        }

        private TreeViewItemData<GridRow> BuildTreeItem(Scry.Core.DataRecord record, Scry.Core.Schema schema)
        {
            var hasCollectionField = false;
            foreach (var field in schema.Fields)
            {
                if (field.Type == Scry.Core.FieldType.Collection)
                {
                    hasCollectionField = true;
                    break;
                }
            }

            if (!hasCollectionField)
                return new TreeViewItemData<GridRow>(GetOrCreateId(record.Id), new GridRow(record, isTopLevel: true));

            var detailId = GetOrCreateId($"{record.Id}#detail");
            var detailChild = new TreeViewItemData<GridRow>(detailId, new GridRow(record, isTopLevel: false));
            var children = new List<TreeViewItemData<GridRow>> { detailChild };
            return new TreeViewItemData<GridRow>(GetOrCreateId(record.Id), new GridRow(record, isTopLevel: true), children);
        }

        private VisualElement BuildDetailPane(GridState state, MultiColumnTreeView treeView, Scry.Core.DataRecord record)
        {
            var container = new VisualElement();

            foreach (var field in state.Collection.Schema.Fields)
            {
                if (field.Type != Scry.Core.FieldType.Collection)
                    continue;

                container.Add(BuildCollectionFieldPane(state, treeView, record, field));
            }

            return container;
        }

        private VisualElement BuildCollectionFieldPane(GridState state, MultiColumnTreeView treeView, Scry.Core.DataRecord record, Scry.Core.FieldDescriptor field)
        {
            var pane = new VisualElement();
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            header.Add(new Label(field.Name) { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
            var addButton = new Button(() =>
            {
                try
                {
                    var updated = _repository.AddCollectionEntry(record, field.Name, state.ScriptableObjectType);
                    state.ReplaceRecord(updated);
                    RefreshTreeItemsInPlace(treeView, state);
                }
                catch (Scry.Core.Unity.WriteConflictException)
                {
                    PromptReloadOnConflict(state, treeView, record.Id);
                }
            })
            { text = "+" };
            header.Add(addButton);
            pane.Add(header);

            // Issues rolled up onto this Collection field split into two kinds: a "#index" issue
            // (e.g. NoDuplicateRule) points at one specific entry and is decorated on that entry's
            // row below; anything else (an ungrouped or grouped SumEqualsRule mismatch) is an
            // aggregate across multiple entries with no single row to attach to, so it renders as
            // a message directly under the header instead - this is the exact case that used to
            // produce zero visual decoration anywhere (the "drop weights sum to 100" dogfooding case).
            var collectionFieldIssues = state.Issues.IssuesForCollectionField(record.Id, field.Name);
            foreach (var issue in collectionFieldIssues)
            {
                if (ValidationIssueLocator.Locate(issue).ChildIndex.HasValue)
                    continue;

                pane.Add(new Label(issue.Message)
                {
                    style =
                    {
                        color = issue.Severity == Scry.Core.ValidationSeverity.Error ? Color.red : Color.yellow,
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                });
            }

            var entries = record.GetValue(field.Name) as IReadOnlyList<Scry.Core.DataRecord> ?? new List<Scry.Core.DataRecord>();
            var elementFields = new List<Scry.Core.FieldDescriptor>();
            foreach (var elementField in field.ElementSchema.Fields)
            {
                if (elementField.IsSupported)
                    elementFields.Add(elementField);
            }

            var subColumns = new Columns();
            foreach (var elementField in elementFields)
            {
                subColumns.Add(new Column
                {
                    name = elementField.Name,
                    title = elementField.Name,
                    makeCell = () => new VisualElement { style = { flexDirection = FlexDirection.Row } },
                    bindCell = (container, entryIndex) =>
                    {
                        container.Clear();
                        var entryRecord = entries[entryIndex];
                        var cell = CellBinder.CreateCell(elementField);
                        CellBinder.BindCell(cell, elementField, entryRecord, newValue =>
                        {
                            try
                            {
                                var updated = EditGateway.ApplyNestedEdit(_repository, record, field.Name, entryIndex, elementField.Name, newValue, state.ScriptableObjectType, $"Edit {elementField.Name}");
                                state.ReplaceRecord(updated);
                                RefreshTreeItemsInPlace(treeView, state);
                            }
                            catch (Scry.Core.Unity.WriteConflictException)
                            {
                                PromptReloadOnConflict(state, treeView, record.Id);
                            }
                        });
                        container.Add(cell);

                        var entryIssues = collectionFieldIssues
                            .Where(i => ValidationIssueLocator.Locate(i).ChildIndex == entryIndex && i.FieldName == elementField.Name)
                            .ToList();
                        if (entryIssues.Count > 0)
                        {
                            var entrySeverity = entryIssues.Any(i => i.Severity == Scry.Core.ValidationSeverity.Error)
                                ? Scry.Core.ValidationSeverity.Error
                                : Scry.Core.ValidationSeverity.Warning;
                            var icon = new Label(entrySeverity == Scry.Core.ValidationSeverity.Error ? "!" : "?")
                            {
                                tooltip = string.Join("\n", entryIssues.Select(i => i.Message)),
                                style =
                                {
                                    color = entrySeverity == Scry.Core.ValidationSeverity.Error ? Color.red : Color.yellow,
                                    unityFontStyleAndWeight = FontStyle.Bold
                                }
                            };
                            container.Add(icon);
                        }
                    }
                });
            }

            subColumns.Add(new Column
            {
                name = "__remove",
                title = string.Empty,
                width = 30,
                makeCell = () => new Button { text = "-" },
                bindCell = (cell, entryIndex) =>
                {
                    var button = (Button)cell;
                    button.clickable = new Clickable(() =>
                    {
                        try
                        {
                            var updated = _repository.RemoveCollectionEntry(record, field.Name, entryIndex, state.ScriptableObjectType);
                            state.ReplaceRecord(updated);
                            RefreshTreeItemsInPlace(treeView, state);
                        }
                        catch (Scry.Core.Unity.WriteConflictException)
                        {
                            PromptReloadOnConflict(state, treeView, record.Id);
                        }
                    });
                }
            });

            var subGrid = new MultiColumnListView(subColumns)
            {
                itemsSource = (System.Collections.IList)entries,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
            };
            pane.Add(subGrid);

            return pane;
        }
    }
}
