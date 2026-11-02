using System;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Переходы задания по графу. Мгновенные узлы (Start, Condition) проходятся сразу; Objective ждёт событий;
    /// End завершает продвижение. Состояние задания и награды — забота <see cref="QuestManager"/>.
    /// </summary>
    internal sealed class QuestGraphRunner
    {
        public const int MaxStepsPerAdvance = 1000;

        private readonly IQuestConditionContext conditionContext;

        public QuestGraphRunner(IQuestConditionContext conditionContext)
        {
            this.conditionContext = conditionContext;
        }

        public QuestAdvanceOutcome Begin(QuestInstance instance, out string error)
        {
            StartNodeData start = null;
            foreach (var node in instance.Graph.Nodes)
            {
                if (node is StartNodeData candidate)
                {
                    start = candidate;
                    break;
                }
            }

            if (start == null)
            {
                error = "The graph has no Start node.";
                return QuestAdvanceOutcome.Broken;
            }

            instance.CurrentNodeId = start.Id;
            return Advance(instance, out error);
        }

        /// <summary>
        /// Продвигает задание от текущего узла до первой незавершённой цели или до End.
        /// </summary>
        public QuestAdvanceOutcome Advance(QuestInstance instance, out string error)
        {
            var graph = instance.Graph;
            for (var step = 0; step < MaxStepsPerAdvance; step++)
            {
                var node = graph.FindNode(instance.CurrentNodeId);
                switch (node)
                {
                    case null:
                        error = $"Node '{instance.CurrentNodeId}' was not found in the graph.";
                        return QuestAdvanceOutcome.Broken;

                    case EndNodeData end:
                        error = null;
                        return end.Result == QuestEndResult.Completed
                            ? QuestAdvanceOutcome.ReachedCompletedEnd
                            : QuestAdvanceOutcome.ReachedFailedEnd;

                    case ObjectiveNodeData objective:
                        if (instance.GetProgress(objective.ObjectiveId) < objective.RequiredCount)
                        {
                            error = null;
                            return QuestAdvanceOutcome.WaitingForObjective;
                        }

                        if (!MoveAlong(instance, node, QuestPortIds.Out, out error))
                        {
                            return QuestAdvanceOutcome.Broken;
                        }

                        break;

                    case ConditionNodeData condition:
                        bool result;
                        try
                        {
                            result = condition.Evaluate(conditionContext);
                        }
                        catch (Exception exception)
                        {
                            error = $"Condition '{condition.GetSummary()}' threw an exception: {exception.Message}";
                            return QuestAdvanceOutcome.Broken;
                        }

                        if (!MoveAlong(instance, node, result ? QuestPortIds.True : QuestPortIds.False, out error))
                        {
                            return QuestAdvanceOutcome.Broken;
                        }

                        break;

                    case StartNodeData _:
                        if (!MoveAlong(instance, node, QuestPortIds.Out, out error))
                        {
                            return QuestAdvanceOutcome.Broken;
                        }

                        break;

                    default:
                        error = $"A node of type {node.GetType().Name} cannot be part of the quest flow.";
                        return QuestAdvanceOutcome.Broken;
                }
            }

            error = $"Exceeded {MaxStepsPerAdvance} transitions in one step; the graph may contain a cycle.";
            return QuestAdvanceOutcome.Broken;
        }

        /// <summary>
        /// Применяет событие к текущей цели. Прогресс не превышает требуемый. Возвращает true, если прогресс изменился.
        /// </summary>
        public bool TryApplyEvent(QuestInstance instance, in QuestEvent questEvent, out ObjectiveNodeData objective, out int newProgress)
        {
            objective = instance.CurrentObjective;
            newProgress = 0;
            if (objective == null || !objective.TryGetProgress(questEvent, out var delta) || delta <= 0)
            {
                return false;
            }

            var current = instance.GetProgress(objective.ObjectiveId);
            var required = objective.RequiredCount;
            if (current >= required)
            {
                return false;
            }

            newProgress = (int)Math.Min(required, (long)current + delta);
            instance.SetProgress(objective.ObjectiveId, newProgress);
            return true;
        }

        private static bool MoveAlong(QuestInstance instance, QuestNodeData node, string portId, out string error)
        {
            foreach (var edge in instance.Graph.Edges)
            {
                if (edge != null
                    && string.Equals(edge.FromNodeId, node.Id, StringComparison.Ordinal)
                    && string.Equals(edge.FromPortId, portId, StringComparison.Ordinal))
                {
                    instance.CurrentNodeId = edge.ToNodeId;
                    error = null;
                    return true;
                }
            }

            error = $"Output '{portId}' of node '{node.GetSummary()}' is not connected.";
            return false;
        }
    }
}
