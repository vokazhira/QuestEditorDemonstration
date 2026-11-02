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
                reason = "Узел не найден.";
                return false;
            }

            if (fromPort == null || toPort == null)
            {
                reason = "Порт не найден.";
                return false;
            }

            if (fromPort.Direction != QuestPortDirection.Output || toPort.Direction != QuestPortDirection.Input)
            {
                reason = "Соединять можно только выход со входом.";
                return false;
            }

            if (fromPort.Kind != toPort.Kind)
            {
                reason = $"Нельзя соединить порт вида {fromPort.Kind} с портом вида {toPort.Kind}.";
                return false;
            }

            if (ReferenceEquals(fromNode, toNode) || string.Equals(fromNode.Id, toNode.Id, StringComparison.Ordinal))
            {
                reason = "Нельзя соединить узел сам с собой.";
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
                reason = "Граф не задан.";
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
                reason = "Такая связь уже существует.";
                return false;
            }

            if (fromPort.Capacity == QuestPortCapacity.Single && graph.CountEdgesFrom(fromNodeId, fromPortId) > 0)
            {
                reason = $"Выход «{fromPort.DisplayName}» уже подключён.";
                return false;
            }

            if (toPort.Capacity == QuestPortCapacity.Single && graph.CountEdgesTo(toNodeId, toPortId) > 0)
            {
                reason = $"Вход «{toPort.DisplayName}» уже подключён.";
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
