using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Правила соединения портов. Общие для редактора графа и валидатора.
    /// </summary>
    public static class QuestPortRules
    {
        /// <summary>
        /// Проверяет совместимость пары портов без учёта уже существующих связей:
        /// направление, вид порта и запрет соединения узла с самим собой.
        /// </summary>
        public static bool CanConnect(
            QuestNodeData fromNode,
            QuestPortDefinition fromPort,
            QuestNodeData toNode,
            QuestPortDefinition toPort,
            out string reason)
        {
            if (fromNode == null || toNode == null)
            {
                reason = "Node not found.";
                return false;
            }

            if (fromPort == null || toPort == null)
            {
                reason = "Port not found.";
                return false;
            }

            if (fromPort.Direction != QuestPortDirection.Output || toPort.Direction != QuestPortDirection.Input)
            {
                reason = "Only an output can be connected to an input.";
                return false;
            }

            if (fromPort.Kind != toPort.Kind)
            {
                reason = $"A {fromPort.Kind} port cannot be connected to a {toPort.Kind} port.";
                return false;
            }

            if (ReferenceEquals(fromNode, toNode) || string.Equals(fromNode.Id, toNode.Id, StringComparison.Ordinal))
            {
                reason = "A node cannot be connected to itself.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Полная проверка новой связи в графе: существование узлов и портов, совместимость,
        /// отсутствие дубликата и соблюдение capacity. Циклы не проверяются.
        /// </summary>
        public static bool CanConnect(
            QuestGraphAsset graph,
            string fromNodeId,
            string fromPortId,
            string toNodeId,
            string toPortId,
            out string reason)
        {
            if (graph == null)
            {
                reason = "No graph.";
                return false;
            }

            var fromNode = graph.FindNode(fromNodeId);
            var toNode = graph.FindNode(toNodeId);
            var fromPort = fromNode?.FindPort(fromPortId);
            var toPort = toNode?.FindPort(toPortId);

            if (!CanConnect(fromNode, fromPort, toNode, toPort, out reason))
            {
                return false;
            }

            if (graph.HasEdge(fromNodeId, fromPortId, toNodeId, toPortId))
            {
                reason = "This connection already exists.";
                return false;
            }

            if (fromPort.Capacity == QuestPortCapacity.Single && graph.CountEdgesFrom(fromNodeId, fromPortId) > 0)
            {
                reason = $"Output '{fromPort.DisplayName}' is already connected.";
                return false;
            }

            if (toPort.Capacity == QuestPortCapacity.Single && graph.CountEdgesTo(toNodeId, toPortId) > 0)
            {
                reason = $"Input '{toPort.DisplayName}' is already connected.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Проверяет, замкнёт ли новая Flow-связь <paramref name="fromNodeId"/> → <paramref name="toNodeId"/> цикл:
        /// то есть достижим ли <paramref name="fromNodeId"/> из <paramref name="toNodeId"/> по существующим Flow-связям.
        /// Связи наград не учитываются.
        /// </summary>
        public static bool WouldCreateFlowCycle(QuestGraphAsset graph, string fromNodeId, string toNodeId)
        {
            if (graph == null || string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(toNodeId))
            {
                return false;
            }

            if (string.Equals(fromNodeId, toNodeId, StringComparison.Ordinal))
            {
                return true;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal) { toNodeId };
            var pending = new Stack<string>();
            pending.Push(toNodeId);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var edge in graph.Edges)
                {
                    if (edge == null || !string.Equals(edge.FromNodeId, current, StringComparison.Ordinal) || !IsFlowEdge(graph, edge))
                    {
                        continue;
                    }

                    if (string.Equals(edge.ToNodeId, fromNodeId, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (visited.Add(edge.ToNodeId))
                    {
                        pending.Push(edge.ToNodeId);
                    }
                }
            }

            return false;
        }

        private static bool IsFlowEdge(QuestGraphAsset graph, QuestEdgeData edge)
        {
            var port = graph.FindNode(edge.FromNodeId)?.FindPort(edge.FromPortId);
            return port != null && port.Kind == QuestPortKind.Flow;
        }
    }
}
