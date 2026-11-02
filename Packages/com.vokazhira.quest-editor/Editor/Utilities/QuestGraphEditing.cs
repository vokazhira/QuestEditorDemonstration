using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Все изменения графа из редактора. Каждое действие записывается в Undo до изменения модели.
    /// </summary>
    internal static class QuestGraphEditing
    {
        public static void AddNode(QuestGraphAsset graph, QuestNodeData node)
        {
            Undo.RecordObject(graph, "Add Quest Node");
            graph.AddNode(node);
        }

        public static bool MoveNodes(QuestGraphAsset graph, IReadOnlyList<KeyValuePair<string, Vector2>> positions)
        {
            var changed = false;
            foreach (var pair in positions)
            {
                var node = graph.FindNode(pair.Key);
                if (node == null || node.Position == pair.Value)
                {
                    continue;
                }

                if (!changed)
                {
                    Undo.RecordObject(graph, "Move Quest Nodes");
                    changed = true;
                }

                node.Position = pair.Value;
            }

            return changed;
        }

        /// <summary>
        /// Добавляет связь, если её разрешают <see cref="QuestPortRules"/> и она не замыкает Flow-цикл.
        /// При отказе модель не меняется и Undo не записывается.
        /// </summary>
        public static bool TryConnect(QuestGraphAsset graph, QuestEdgeData edge, out string reason)
        {
            if (!QuestPortRules.CanConnect(graph, edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId, out reason))
            {
                return false;
            }

            var fromPort = graph.FindNode(edge.FromNodeId).FindPort(edge.FromPortId);
            if (fromPort.Kind == QuestPortKind.Flow && QuestPortRules.WouldCreateFlowCycle(graph, edge.FromNodeId, edge.ToNodeId))
            {
                reason = "The connection creates a cycle; cycles in the quest flow are not allowed.";
                return false;
            }

            Undo.RecordObject(graph, "Connect Quest Nodes");
            return graph.TryConnect(edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId, out reason);
        }

        public static bool Disconnect(QuestGraphAsset graph, IReadOnlyList<QuestEdgeData> edges)
        {
            if (edges.Count == 0)
            {
                return false;
            }

            Undo.RecordObject(graph, edges.Count == 1 ? "Delete Quest Edge" : "Delete Quest Edges");
            var removed = false;
            foreach (var edge in edges)
            {
                removed |= graph.Disconnect(edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId);
            }

            return removed;
        }

        /// <summary>
        /// Удаляет связи узла, которые ссылаются на порты, которых у узлов больше нет
        /// (например, у End сменили результат на Failed и пропал порт наград). Возвращает число удалённых связей.
        /// </summary>
        public static int RemoveDanglingEdges(QuestGraphAsset graph, string nodeId)
        {
            var dangling = new List<QuestEdgeData>();
            foreach (var edge in graph.Edges)
            {
                if (edge == null || !edge.Touches(nodeId))
                {
                    continue;
                }

                var fromPort = graph.FindNode(edge.FromNodeId)?.FindPort(edge.FromPortId);
                var toPort = graph.FindNode(edge.ToNodeId)?.FindPort(edge.ToPortId);
                if (fromPort == null || toPort == null)
                {
                    dangling.Add(edge);
                }
            }

            if (dangling.Count == 0)
            {
                return 0;
            }

            Disconnect(graph, dangling);
            return dangling.Count;
        }

        public static bool RemoveNodes(QuestGraphAsset graph, IReadOnlyCollection<string> nodeIds)
        {
            if (nodeIds.Count == 0)
            {
                return false;
            }

            Undo.RecordObject(graph, nodeIds.Count == 1 ? "Delete Quest Node" : "Delete Quest Nodes");
            var removed = false;
            foreach (var id in nodeIds)
            {
                removed |= graph.RemoveNode(id);
            }

            return removed;
        }
    }
}
