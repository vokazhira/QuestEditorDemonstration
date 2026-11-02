using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Холст графа. Не хранит собственных данных: всё строится из <see cref="QuestGraphAsset"/>,
    /// а изменения пользователя переносятся в модель через <see cref="QuestGraphEditing"/>.
    /// </summary>
    internal sealed class QuestGraphView : GraphView
    {
        private const string StyleSheetPath = "Packages/com.vokazhira.quest-editor/Editor/GraphView/QuestGraphView.uss";

        private readonly EditorWindow window;
        private readonly Dictionary<string, QuestNodeView> nodeViews = new Dictionary<string, QuestNodeView>();
        private QuestNodeSearchProvider searchProvider;
        private QuestGraphAsset graph;
        private bool isRebuilding;

        public QuestGraphView(EditorWindow window)
        {
            this.window = window;
            AddToClassList("qe-graph");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"Quest Editor: stylesheet not found at '{StyleSheetPath}'.");
            }

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            graphViewChanged = OnGraphViewChanged;
            nodeCreationRequest = OnNodeCreationRequest;

            RegisterCallback<DetachFromPanelEvent>(_ => DestroySearchProvider());
        }

        public event Action ModelChanged;

        public QuestGraphAsset Graph => graph;

        public void SetGraph(QuestGraphAsset value)
        {
            graph = value;
            Rebuild();
        }

        /// <summary>
        /// Полностью пересобирает представление из модели, сохраняя выделение по Id узлов.
        /// </summary>
        public void Rebuild()
        {
            var selectedIds = new HashSet<string>(selection.OfType<QuestNodeView>().Select(v => v.NodeId));

            isRebuilding = true;
            try
            {
                foreach (var element in graphElements.ToList())
                {
                    RemoveElement(element);
                }

                nodeViews.Clear();
                if (graph == null)
                {
                    return;
                }

                graph.InvalidateNodeIndex();
                foreach (var node in graph.Nodes)
                {
                    if (node != null && !string.IsNullOrEmpty(node.Id) && !nodeViews.ContainsKey(node.Id))
                    {
                        AddNodeView(node);
                    }
                }

                foreach (var edge in graph.Edges)
                {
                    AddEdgeView(edge);
                }

                foreach (var id in selectedIds)
                {
                    if (nodeViews.TryGetValue(id, out var view))
                    {
                        AddToSelection(view);
                    }
                }
            }
            finally
            {
                isRebuilding = false;
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return new List<Port>();
        }

        private QuestNodeView AddNodeView(QuestNodeData node)
        {
            var view = new QuestNodeView(node);
            nodeViews.Add(node.Id, view);
            AddElement(view);
            return view;
        }

        private void AddEdgeView(QuestEdgeData edgeData)
        {
            if (edgeData == null
                || !nodeViews.TryGetValue(edgeData.FromNodeId ?? string.Empty, out var fromView)
                || !nodeViews.TryGetValue(edgeData.ToNodeId ?? string.Empty, out var toView))
            {
                return;
            }

            var output = fromView.FindPort(edgeData.FromPortId);
            var input = toView.FindPort(edgeData.ToPortId);
            if (output == null || input == null)
            {
                return;
            }

            var edge = output.ConnectTo(input);
            edge.capabilities &= ~(Capabilities.Deletable | Capabilities.Selectable | Capabilities.Copiable);
            AddElement(edge);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (isRebuilding || graph == null)
            {
                return change;
            }

            var modelChanged = false;

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                modelChanged |= ApplyRemovals(change.elementsToRemove);
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                var positions = change.movedElements
                    .OfType<QuestNodeView>()
                    .Select(v => new KeyValuePair<string, Vector2>(v.NodeId, v.GetPosition().position))
                    .ToList();
                modelChanged |= QuestGraphEditing.MoveNodes(graph, positions);
            }

            if (modelChanged)
            {
                ModelChanged?.Invoke();
            }

            return change;
        }

        private bool ApplyRemovals(List<GraphElement> elementsToRemove)
        {
            var removedViews = elementsToRemove.OfType<QuestNodeView>().ToList();
            var startIds = graph.Nodes.OfType<StartNodeData>().Select(n => n.Id).ToList();
            var removedStarts = removedViews.Where(v => startIds.Contains(v.NodeId)).ToList();
            if (startIds.Count > 0 && removedStarts.Count == startIds.Count)
            {
                var keep = removedStarts[0];
                elementsToRemove.Remove(keep);
                removedViews.Remove(keep);
                elementsToRemove.RemoveAll(e => e is Edge edge && (edge.input?.node == keep || edge.output?.node == keep));
                window.ShowNotification(new GUIContent("Нельзя удалить единственный узел Start."));
            }

            var removedIds = new HashSet<string>(removedViews.Select(v => v.NodeId));
            elementsToRemove.RemoveAll(e => e is Edge edge && !IsAttachedToRemovedNode(edge, removedIds));

            foreach (var id in removedIds)
            {
                nodeViews.Remove(id);
            }

            return QuestGraphEditing.RemoveNodes(graph, removedIds);
        }

        private static bool IsAttachedToRemovedNode(Edge edge, HashSet<string> removedIds)
        {
            return (edge.input?.node is QuestNodeView input && removedIds.Contains(input.NodeId))
                || (edge.output?.node is QuestNodeView output && removedIds.Contains(output.NodeId));
        }

        private void OnNodeCreationRequest(NodeCreationContext context)
        {
            if (graph == null)
            {
                return;
            }

            if (searchProvider == null)
            {
                searchProvider = ScriptableObject.CreateInstance<QuestNodeSearchProvider>();
                searchProvider.hideFlags = HideFlags.HideAndDontSave;
                searchProvider.NodeTypeSelected = CreateNode;
            }

            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchProvider);
        }

        private void CreateNode(Type type, Vector2 screenMousePosition)
        {
            if (graph == null || !QuestNodeTypeCache.IsCreatable(type))
            {
                return;
            }

            var node = (QuestNodeData)Activator.CreateInstance(type);
            node.Position = ScreenToGraphPosition(screenMousePosition);
            QuestGraphEditing.AddNode(graph, node);

            var view = AddNodeView(node);
            ClearSelection();
            AddToSelection(view);
            ModelChanged?.Invoke();
        }

        private Vector2 ScreenToGraphPosition(Vector2 screenMousePosition)
        {
            var windowRoot = window.rootVisualElement;
            var windowPosition = windowRoot.ChangeCoordinatesTo(windowRoot.parent, screenMousePosition - window.position.position);
            return contentViewContainer.WorldToLocal(windowPosition);
        }

        private void DestroySearchProvider()
        {
            if (searchProvider != null)
            {
                Object.DestroyImmediate(searchProvider);
                searchProvider = null;
            }
        }
    }
}
