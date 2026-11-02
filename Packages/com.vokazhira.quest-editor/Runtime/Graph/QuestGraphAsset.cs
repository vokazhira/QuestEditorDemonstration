using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Определение задания: узлы, связи и настройки. В рантайме только читается.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuest", menuName = "Quest Editor/Quest Graph", order = 0)]
    public sealed class QuestGraphAsset : ScriptableObject
    {
        public const int CurrentFormatVersion = 1;

        [SerializeField, Tooltip("Уникальный идентификатор задания. Используется в сохранениях и условиях других заданий.")]
        private string questId = string.Empty;

        [SerializeField, Tooltip("Название задания для UI.")]
        private string displayName = string.Empty;

        [SerializeField, TextArea, Tooltip("Описание задания для UI.")]
        private string description = string.Empty;

        [SerializeField, HideInInspector]
        private int formatVersion = CurrentFormatVersion;

        [SerializeField, Tooltip("AutoGrant — награды выдаются сразу при завершении; ManualClaim — по вызову Claim.")]
        private QuestRewardMode rewardMode = QuestRewardMode.AutoGrant;

        [SerializeReference]
        private List<QuestNodeData> nodes = new List<QuestNodeData>();

        [SerializeField]
        private List<QuestEdgeData> edges = new List<QuestEdgeData>();

        [NonSerialized]
        private Dictionary<string, int> nodeIndexById;

        public string QuestId
        {
            get => questId;
            set => questId = value ?? string.Empty;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value ?? string.Empty;
        }

        public string Description
        {
            get => description;
            set => description = value ?? string.Empty;
        }

        public int FormatVersion => formatVersion;

        public QuestRewardMode RewardMode
        {
            get => rewardMode;
            set => rewardMode = value;
        }

        /// <summary>
        /// Узлы графа. Элемент может быть null, если тип узла был удалён из проекта.
        /// </summary>
        public IReadOnlyList<QuestNodeData> Nodes => nodes;

        public IReadOnlyList<QuestEdgeData> Edges => edges;

        /// <summary>
        /// Находит узел по Id. Индекс строится лениво и перестраивается, если устарел
        /// (например, после Undo или изменения через SerializedObject).
        /// </summary>
        public QuestNodeData FindNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return null;
            }

            if (TryGetIndexedNode(nodeId, out var node))
            {
                return node;
            }

            RebuildNodeIndex();
            TryGetIndexedNode(nodeId, out node);
            return node;
        }

        public void AddNode(QuestNodeData node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (FindNode(node.Id) != null)
            {
                throw new ArgumentException($"Node with id '{node.Id}' already exists in the graph.", nameof(node));
            }

            nodes.Add(node);
            InvalidateNodeIndex();
        }

        /// <summary>
        /// Удаляет узел вместе со всеми его связями.
        /// </summary>
        public bool RemoveNode(string nodeId)
        {
            var index = nodes.FindIndex(n => n != null && string.Equals(n.Id, nodeId, StringComparison.Ordinal));
            if (index < 0)
            {
                return false;
            }

            nodes.RemoveAt(index);
            edges.RemoveAll(e => e == null || e.Touches(nodeId));
            InvalidateNodeIndex();
            return true;
        }

        /// <summary>
        /// Добавляет связь, если её разрешают <see cref="QuestPortRules"/>.
        /// Циклы здесь не проверяются — это задача валидатора.
        /// </summary>
        public bool TryConnect(string fromNodeId, string fromPortId, string toNodeId, string toPortId, out string reason)
        {
            if (!QuestPortRules.CanConnect(this, fromNodeId, fromPortId, toNodeId, toPortId, out reason))
            {
                return false;
            }

            edges.Add(new QuestEdgeData(fromNodeId, fromPortId, toNodeId, toPortId));
            return true;
        }

        public bool Disconnect(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            return edges.RemoveAll(e => e != null && e.Matches(fromNodeId, fromPortId, toNodeId, toPortId)) > 0;
        }

        public bool HasEdge(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            for (var i = 0; i < edges.Count; i++)
            {
                if (edges[i] != null && edges[i].Matches(fromNodeId, fromPortId, toNodeId, toPortId))
                {
                    return true;
                }
            }

            return false;
        }

        public int CountEdgesFrom(string nodeId, string portId)
        {
            var count = 0;
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge != null
                    && string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal)
                    && string.Equals(edge.FromPortId, portId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public int CountEdgesTo(string nodeId, string portId)
        {
            var count = 0;
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge != null
                    && string.Equals(edge.ToNodeId, nodeId, StringComparison.Ordinal)
                    && string.Equals(edge.ToPortId, portId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public void InvalidateNodeIndex()
        {
            nodeIndexById = null;
        }

        private void OnValidate()
        {
            InvalidateNodeIndex();
        }

        private bool TryGetIndexedNode(string nodeId, out QuestNodeData node)
        {
            node = null;
            if (nodeIndexById == null || !nodeIndexById.TryGetValue(nodeId, out var index))
            {
                return false;
            }

            if (index < 0 || index >= nodes.Count)
            {
                return false;
            }

            var candidate = nodes[index];
            if (candidate == null || !string.Equals(candidate.Id, nodeId, StringComparison.Ordinal))
            {
                return false;
            }

            node = candidate;
            return true;
        }

        private void RebuildNodeIndex()
        {
            nodeIndexById = new Dictionary<string, int>(nodes.Count, StringComparer.Ordinal);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node != null && !string.IsNullOrEmpty(node.Id) && !nodeIndexById.ContainsKey(node.Id))
                {
                    nodeIndexById.Add(node.Id, i);
                }
            }
        }
    }
}
