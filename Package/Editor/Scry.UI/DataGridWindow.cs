using System;
using System.Collections.Generic;
using Scry.Core.Unity;
using Scry.Core.Unity.Config;
using UnityEditor;
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

            if (type == null)
            {
                _content.Add(new Label($"Could not resolve type '{tracked.TypeName}'."));
                return;
            }

            var collection = _repository.Scan(type);
            _activeState = new GridState(type, collection, tracked.Rules);
            _content.Add(BuildGrid(_activeState));
        }

        private int GetOrCreateId(string recordId)
        {
            if (_idsByRecordId.TryGetValue(recordId, out var id))
                return id;

            id = _nextId++;
            _idsByRecordId[recordId] = id;
            return id;
        }

        private VisualElement BuildGrid(GridState state)
        {
            // Task 8 replaces this with the real MultiColumnTreeView.
            return new Label($"{state.Collection.Records.Count} record(s) of '{state.ScriptableObjectType.Name}' (grid not built yet).");
        }
    }
}
