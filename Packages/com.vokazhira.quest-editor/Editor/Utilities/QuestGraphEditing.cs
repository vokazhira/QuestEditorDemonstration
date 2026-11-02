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
