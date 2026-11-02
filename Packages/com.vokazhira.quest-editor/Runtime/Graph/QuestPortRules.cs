using System;

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
    }
}
