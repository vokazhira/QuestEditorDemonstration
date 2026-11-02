using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Проверяет граф задания. Не зависит от UI и редактора: используется окном редактора при Validate и Save
    /// и рантаймом перед запуском. Граф с хотя бы одной ошибкой (Error) исполнять нельзя.
    /// </summary>
    public static class QuestGraphValidator
    {
        private readonly struct ResolvedEdge
        {
            public ResolvedEdge(QuestEdgeData data, QuestNodeData from, QuestPortDefinition fromPort, QuestNodeData to, QuestPortDefinition toPort)
            {
                Data = data;
                From = from;
                FromPort = fromPort;
                To = to;
                ToPort = toPort;
            }

            public QuestEdgeData Data { get; }
            public QuestNodeData From { get; }
            public QuestPortDefinition FromPort { get; }
            public QuestNodeData To { get; }
            public QuestPortDefinition ToPort { get; }
        }

        /// <summary>
        /// Проверяет <paramref name="graph"/>. <paramref name="otherQuests"/> — остальные задания для проверки
        /// уникальности questId (сам граф в списке игнорируется). Результат отсортирован: ошибки, предупреждения, информация.
        /// </summary>
        public static List<QuestDiagnostic> Validate(QuestGraphAsset graph, IReadOnlyList<QuestGraphAsset> otherQuests = null)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var diagnostics = new List<QuestDiagnostic>();
            graph.InvalidateNodeIndex();

            ValidateQuestId(graph, otherQuests, diagnostics);
            var nodes = CollectNodes(graph, diagnostics, out var nodesById);
            ValidateStartAndEnd(nodes, diagnostics);
            ValidateUniqueGameplayIds(nodes, diagnostics);
            ValidateNodeFields(graph, nodes, diagnostics);
            var edges = ResolveEdges(graph, nodesById, diagnostics);
            ValidateCapacityAndDuplicates(edges, diagnostics);
            ValidateConnections(nodes, edges, diagnostics);
            ValidateFlow(nodes, edges, diagnostics);

            return SortBySeverity(diagnostics);
        }

        public static bool HasErrors(IEnumerable<QuestDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return false;
            }

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity == QuestDiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateQuestId(QuestGraphAsset graph, IReadOnlyList<QuestGraphAsset> otherQuests, List<QuestDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(graph.QuestId))
            {
                diagnostics.Add(Error(QuestDiagnosticCodes.EmptyQuestId, "The quest has no questId."));
                return;
            }

            if (otherQuests == null)
            {
                return;
            }

            foreach (var other in otherQuests)
            {
                if (other != null && !ReferenceEquals(other, graph) && string.Equals(other.QuestId, graph.QuestId, StringComparison.Ordinal))
                {
                    diagnostics.Add(Error(QuestDiagnosticCodes.DuplicateQuestId, $"questId '{graph.QuestId}' is already used by quest '{other.name}'."));
                }
            }
        }

        private static List<QuestNodeData> CollectNodes(QuestGraphAsset graph, List<QuestDiagnostic> diagnostics, out Dictionary<string, QuestNodeData> nodesById)
        {
            var nodes = new List<QuestNodeData>();
            nodesById = new Dictionary<string, QuestNodeData>(StringComparer.Ordinal);
            var reportedDuplicates = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (node == null)
                {
                    diagnostics.Add(Error(
                        QuestDiagnosticCodes.MissingNodeType,
                        $"Node #{i + 1}: node type not found (the class was deleted or renamed without [MovedFrom])."));
                    continue;
                }

                nodes.Add(node);
                if (string.IsNullOrEmpty(node.Id))
                {
                    diagnostics.Add(Error(QuestDiagnosticCodes.InvalidValue, "A node has an empty Id.", null));
                    continue;
                }

                if (nodesById.ContainsKey(node.Id))
                {
                    if (reportedDuplicates.Add(node.Id))
                    {
                        diagnostics.Add(Error(QuestDiagnosticCodes.DuplicateNodeId, $"Node Id '{node.Id}' is used more than once.", node.Id));
                    }

                    continue;
                }

                nodesById.Add(node.Id, node);
            }

            return nodes;
        }

        private static void ValidateStartAndEnd(List<QuestNodeData> nodes, List<QuestDiagnostic> diagnostics)
        {
            var starts = nodes.FindAll(n => n is StartNodeData);
            if (starts.Count == 0)
            {
                diagnostics.Add(Error(QuestDiagnosticCodes.MissingStart, "The graph has no Start node."));
            }
            else if (starts.Count > 1)
            {
                foreach (var start in starts)
                {
                    diagnostics.Add(Error(QuestDiagnosticCodes.MultipleStarts, $"The graph has {starts.Count} Start nodes; exactly one is required.", start.Id));
                }
            }

            if (!nodes.Exists(n => n is EndNodeData))
            {
                diagnostics.Add(Error(QuestDiagnosticCodes.MissingEnd, "The graph has no End node."));
            }
        }

        private static void ValidateUniqueGameplayIds(List<QuestNodeData> nodes, List<QuestDiagnostic> diagnostics)
        {
            var objectiveOwners = new Dictionary<string, QuestNodeData>(StringComparer.Ordinal);
            var rewardOwners = new Dictionary<string, QuestNodeData>(StringComparer.Ordinal);

            foreach (var node in nodes)
            {
                if (node is ObjectiveNodeData objective && !string.IsNullOrEmpty(objective.ObjectiveId))
                {
                    ReportDuplicate(objectiveOwners, objective.ObjectiveId, node, QuestDiagnosticCodes.DuplicateObjectiveId, "ObjectiveId", diagnostics);
                }
                else if (node is RewardNodeData reward && !string.IsNullOrEmpty(reward.RewardId))
                {
                    ReportDuplicate(rewardOwners, reward.RewardId, node, QuestDiagnosticCodes.DuplicateRewardId, "RewardId", diagnostics);
                }
            }
        }

        private static void ReportDuplicate(
            Dictionary<string, QuestNodeData> owners,
            string id,
            QuestNodeData node,
            string code,
            string label,
            List<QuestDiagnostic> diagnostics)
        {
            if (owners.TryGetValue(id, out var first))
            {
                diagnostics.Add(Error(code, $"{label} '{id}' is already used by node '{first.GetSummary()}'.", node.Id));
            }
            else
            {
                owners.Add(id, node);
            }
        }

        private static void ValidateNodeFields(QuestGraphAsset graph, List<QuestNodeData> nodes, List<QuestDiagnostic> diagnostics)
        {
            var context = new QuestValidationContext(graph, diagnostics);
            foreach (var node in nodes)
            {
                context.Node = node;
                try
                {
                    node.Validate(context);
                }
                catch (Exception exception)
                {
                    diagnostics.Add(Error(
                        QuestDiagnosticCodes.NodeValidationFailed,
                        $"Validation of node {node.GetType().Name} threw an exception: {exception.Message}",
                        node.Id));
                }
            }

            context.Node = null;
        }

        private static List<ResolvedEdge> ResolveEdges(QuestGraphAsset graph, Dictionary<string, QuestNodeData> nodesById, List<QuestDiagnostic> diagnostics)
        {
            var result = new List<ResolvedEdge>();
            foreach (var edge in graph.Edges)
            {
                if (edge == null)
                {
                    continue;
                }

                nodesById.TryGetValue(edge.FromNodeId ?? string.Empty, out var from);
                nodesById.TryGetValue(edge.ToNodeId ?? string.Empty, out var to);
                if (from == null || to == null)
                {
                    diagnostics.Add(Error(
                        QuestDiagnosticCodes.EdgeMissingNode,
                        $"Connection {edge} refers to a missing node.",
                        from?.Id ?? to?.Id));
                    continue;
                }

                var fromPort = from.FindPort(edge.FromPortId);
                var toPort = to.FindPort(edge.ToPortId);
                if (fromPort == null || toPort == null)
                {
                    var missing = fromPort == null ? $"'{edge.FromPortId}' on '{from.GetSummary()}'" : $"'{edge.ToPortId}' on '{to.GetSummary()}'";
                    diagnostics.Add(Error(QuestDiagnosticCodes.EdgeMissingPort, $"A connection refers to a missing port {missing}.", fromPort == null ? from.Id : to.Id));
                    continue;
                }

                if (!QuestPortRules.CanConnect(from, fromPort, to, toPort, out var reason))
                {
                    diagnostics.Add(Error(QuestDiagnosticCodes.IncompatiblePorts, $"Invalid connection '{from.GetSummary()}' -> '{to.GetSummary()}': {reason}", from.Id));
                    continue;
                }

                if (fromPort.Kind == QuestPortKind.Reward && !(to is EndNodeData end && end.Result == QuestEndResult.Completed))
                {
                    diagnostics.Add(Error(
                        QuestDiagnosticCodes.RewardNotToCompletedEnd,
                        $"A reward is connected to '{to.GetSummary()}'. Rewards can only be connected to End(Completed).",
                        from.Id));
                    continue;
                }

                result.Add(new ResolvedEdge(edge, from, fromPort, to, toPort));
            }

            return result;
        }

        private static void ValidateCapacityAndDuplicates(List<ResolvedEdge> edges, List<QuestDiagnostic> diagnostics)
        {
            var outgoing = new Dictionary<string, int>(StringComparer.Ordinal);
            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var edge in edges)
            {
                var key = edge.Data.ToString();
                if (!seen.Add(key))
                {
                    diagnostics.Add(Error(QuestDiagnosticCodes.DuplicateEdge, $"Connection '{edge.From.GetSummary()}' -> '{edge.To.GetSummary()}' is duplicated.", edge.From.Id));
                }

                var fromKey = edge.From.Id + "." + edge.FromPort.PortId;
                outgoing[fromKey] = outgoing.TryGetValue(fromKey, out var outCount) ? outCount + 1 : 1;
                if (edge.FromPort.Capacity == QuestPortCapacity.Single && outgoing[fromKey] == 2)
                {
                    diagnostics.Add(Error(QuestDiagnosticCodes.PortCapacityExceeded, $"Output '{edge.FromPort.DisplayName}' has more than one connection.", edge.From.Id));
                }

                var toKey = edge.To.Id + "." + edge.ToPort.PortId;
                incoming[toKey] = incoming.TryGetValue(toKey, out var inCount) ? inCount + 1 : 1;
                if (edge.ToPort.Capacity == QuestPortCapacity.Single && incoming[toKey] == 2)
                {
                    diagnostics.Add(Error(QuestDiagnosticCodes.PortCapacityExceeded, $"Input '{edge.ToPort.DisplayName}' has more than one connection.", edge.To.Id));
                }
            }
        }

        private static void ValidateConnections(List<QuestNodeData> nodes, List<ResolvedEdge> edges, List<QuestDiagnostic> diagnostics)
        {
            foreach (var node in nodes)
            {
                foreach (var port in node.Ports)
                {
                    if (port.Kind != QuestPortKind.Flow || IsConnected(edges, node, port))
                    {
                        continue;
                    }

                    var side = port.Direction == QuestPortDirection.Input ? "Input" : "Output";
                    diagnostics.Add(Error(QuestDiagnosticCodes.FlowPortNotConnected, $"{side} '{port.DisplayName}' is not connected.", node.Id));
                }

                if (node is RewardNodeData && !edges.Exists(e => ReferenceEquals(e.From, node)))
                {
                    diagnostics.Add(Warning(QuestDiagnosticCodes.RewardNotConnected, "The reward is not connected and will never be granted.", node.Id));
                }

                if (node is EndNodeData end
                    && end.Result == QuestEndResult.Completed
                    && !edges.Exists(e => ReferenceEquals(e.To, node) && e.ToPort.Kind == QuestPortKind.Reward))
                {
                    diagnostics.Add(Warning(QuestDiagnosticCodes.CompletedEndWithoutRewards, "This Completed end has no rewards.", node.Id));
                }
            }
        }

        private static bool IsConnected(List<ResolvedEdge> edges, QuestNodeData node, QuestPortDefinition port)
        {
            foreach (var edge in edges)
            {
                if (port.Direction == QuestPortDirection.Output
                    && ReferenceEquals(edge.From, node)
                    && edge.FromPort.PortId == port.PortId)
                {
                    return true;
                }

                if (port.Direction == QuestPortDirection.Input
                    && ReferenceEquals(edge.To, node)
                    && edge.ToPort.PortId == port.PortId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateFlow(List<QuestNodeData> nodes, List<ResolvedEdge> edges, List<QuestDiagnostic> diagnostics)
        {
            var successors = new Dictionary<QuestNodeData, List<QuestNodeData>>();
            foreach (var node in nodes)
            {
                successors[node] = new List<QuestNodeData>();
            }

            foreach (var edge in edges)
            {
                if (edge.FromPort.Kind == QuestPortKind.Flow)
                {
                    successors[edge.From].Add(edge.To);
                }
            }

            ReportCycles(nodes, successors, diagnostics);

            var starts = nodes.FindAll(n => n is StartNodeData);
            if (starts.Count == 0)
            {
                return;
            }

            var reachable = new HashSet<QuestNodeData>();
            var pending = new Stack<QuestNodeData>();
            foreach (var start in starts)
            {
                if (reachable.Add(start))
                {
                    pending.Push(start);
                }
            }

            while (pending.Count > 0)
            {
                foreach (var next in successors[pending.Pop()])
                {
                    if (reachable.Add(next))
                    {
                        pending.Push(next);
                    }
                }
            }

            if (nodes.Exists(n => n is EndNodeData) && !nodes.Exists(n => n is EndNodeData && reachable.Contains(n)))
            {
                diagnostics.Add(Error(QuestDiagnosticCodes.EndUnreachable, "No End node is reachable from Start.", starts[0].Id));
            }

            foreach (var node in nodes)
            {
                if (!(node is RewardNodeData) && !reachable.Contains(node))
                {
                    diagnostics.Add(Warning(QuestDiagnosticCodes.NodeUnreachable, $"Node '{node.GetSummary()}' is not reachable from Start.", node.Id));
                }
            }
        }

        private static void ReportCycles(List<QuestNodeData> nodes, Dictionary<QuestNodeData, List<QuestNodeData>> successors, List<QuestDiagnostic> diagnostics)
        {
            const int Unvisited = 0;
            const int InProgress = 1;
            const int Done = 2;

            var state = new Dictionary<QuestNodeData, int>();
            foreach (var node in nodes)
            {
                state[node] = Unvisited;
            }

            var reported = new HashSet<QuestNodeData>();
            var stack = new Stack<KeyValuePair<QuestNodeData, int>>();

            foreach (var root in nodes)
            {
                if (state[root] != Unvisited)
                {
                    continue;
                }

                state[root] = InProgress;
                stack.Push(new KeyValuePair<QuestNodeData, int>(root, 0));

                while (stack.Count > 0)
                {
                    var frame = stack.Pop();
                    var node = frame.Key;
                    var nextIndex = frame.Value;
                    var children = successors[node];

                    if (nextIndex >= children.Count)
                    {
                        state[node] = Done;
                        continue;
                    }

                    stack.Push(new KeyValuePair<QuestNodeData, int>(node, nextIndex + 1));
                    var child = children[nextIndex];
                    if (state[child] == InProgress)
                    {
                        if (reported.Add(child))
                        {
                            diagnostics.Add(Error(QuestDiagnosticCodes.FlowCycle, $"The quest flow has a cycle through node '{child.GetSummary()}'.", child.Id));
                        }
                    }
                    else if (state[child] == Unvisited)
                    {
                        state[child] = InProgress;
                        stack.Push(new KeyValuePair<QuestNodeData, int>(child, 0));
                    }
                }
            }
        }

        private static List<QuestDiagnostic> SortBySeverity(List<QuestDiagnostic> diagnostics)
        {
            var sorted = new List<QuestDiagnostic>(diagnostics.Count);
            sorted.AddRange(diagnostics.FindAll(d => d.Severity == QuestDiagnosticSeverity.Error));
            sorted.AddRange(diagnostics.FindAll(d => d.Severity == QuestDiagnosticSeverity.Warning));
            sorted.AddRange(diagnostics.FindAll(d => d.Severity == QuestDiagnosticSeverity.Info));
            return sorted;
        }

        private static QuestDiagnostic Error(string code, string message, string nodeId = null)
        {
            return new QuestDiagnostic(QuestDiagnosticSeverity.Error, code, message, nodeId);
        }

        private static QuestDiagnostic Warning(string code, string message, string nodeId = null)
        {
            return new QuestDiagnostic(QuestDiagnosticSeverity.Warning, code, message, nodeId);
        }
    }
}
