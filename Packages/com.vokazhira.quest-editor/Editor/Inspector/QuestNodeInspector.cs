using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Универсальный инспектор узла: по одному PropertyField на каждое видимое поле узла по пути
    /// <c>nodes.Array.data[i]</c> в SerializedObject рабочей копии. Изменения идут через SerializedObject и попадают в Undo.
    /// Подсказки берутся из [Tooltip] полей.
    /// </summary>
    internal sealed class QuestNodeInspector : VisualElement
    {
        private const string EmptySelectionText = "Select a single node to edit its settings.";

        private readonly Label header;
        private readonly Label subHeader;
        private readonly VisualElement fields;
        private string nodeId;
        private int nodeIndex = -1;

        public QuestNodeInspector()
        {
            AddToClassList("qe-inspector");

            var title = new Label("Inspector");
            title.AddToClassList("qe-inspector__title");
            Add(title);

            header = new Label();
            header.AddToClassList("qe-inspector__header");
            Add(header);

            subHeader = new Label();
            subHeader.AddToClassList("qe-inspector__subheader");
            Add(subHeader);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("qe-inspector__scroll");
            fields = new VisualElement();
            fields.AddToClassList("qe-inspector__fields");
            scroll.Add(fields);
            Add(scroll);

            ShowEmpty(EmptySelectionText);
        }

        /// <summary>
        /// Вызывается после изменения любого поля узла. Аргумент — Id узла.
        /// </summary>
        public event Action<string> NodeChanged;

        public string NodeId => nodeId;

        /// <summary>
        /// Показывает узел. Если узел и его индекс не изменились, а <paramref name="force"/> = false, поля не пересоздаются.
        /// </summary>
        public void Show(SerializedObject serializedGraph, QuestGraphAsset graph, string id, bool force)
        {
            var index = IndexOf(graph, id);
            if (!force && id == nodeId && index == nodeIndex)
            {
                return;
            }

            fields.Unbind();
            fields.Clear();
            nodeId = id;
            nodeIndex = index;

            if (serializedGraph == null || graph == null || index < 0)
            {
                ShowEmpty(EmptySelectionText);
                return;
            }

            serializedGraph.Update();
            var node = graph.Nodes[index];
            var info = QuestNodeTypeCache.GetInfo(node.GetType());
            header.text = info != null ? info.DisplayName : node.GetType().Name;
            subHeader.text = $"{node.GetType().Name} · {node.Id}";

            var properties = GetEditableProperties(serializedGraph, index);
            if (properties.Count == 0)
            {
                fields.Add(new Label("This node has no editable fields.") { name = "no-fields" });
                return;
            }

            var changedId = id;
            foreach (var property in properties)
            {
                var field = new PropertyField(property);
                field.RegisterValueChangeCallback(_ => NodeChanged?.Invoke(changedId));
                fields.Add(field);
            }

            fields.Bind(serializedGraph);
        }

        /// <summary>
        /// Видимые поля узла с индексом <paramref name="nodeIndex"/> (без [HideInInspector]-полей Id и позиции).
        /// </summary>
        public static List<SerializedProperty> GetEditableProperties(SerializedObject serializedGraph, int nodeIndex)
        {
            var result = new List<SerializedProperty>();
            var nodeProperty = serializedGraph.FindProperty($"nodes.Array.data[{nodeIndex}]");
            if (nodeProperty == null || nodeProperty.propertyType != SerializedPropertyType.ManagedReference)
            {
                return result;
            }

            var iterator = nodeProperty.Copy();
            var end = nodeProperty.GetEndProperty();
            if (!iterator.NextVisible(true))
            {
                return result;
            }

            while (!SerializedProperty.EqualContents(iterator, end))
            {
                result.Add(iterator.Copy());
                if (!iterator.NextVisible(false))
                {
                    break;
                }
            }

            return result;
        }

        private static int IndexOf(QuestGraphAsset graph, string id)
        {
            if (graph == null || string.IsNullOrEmpty(id))
            {
                return -1;
            }

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] != null && graph.Nodes[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ShowEmpty(string message)
        {
            header.text = string.Empty;
            subHeader.text = string.Empty;
            fields.Add(new Label(message) { name = "empty" });
        }
    }
}
