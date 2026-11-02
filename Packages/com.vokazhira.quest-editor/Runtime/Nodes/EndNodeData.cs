using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Flow/End", "#757575")]
    public sealed class EndNodeData : QuestNodeData
    {
        private static readonly IReadOnlyList<QuestPortDefinition> CompletedPorts = Array.AsReadOnly(new[]
        {
            QuestPortDefinition.FlowInput(QuestPortIds.In, "In"),
            new QuestPortDefinition(QuestPortIds.Rewards, QuestPortDirection.Input, QuestPortKind.Reward, QuestPortCapacity.Multi, "Rewards")
        });

        private static readonly IReadOnlyList<QuestPortDefinition> FailedPorts = Array.AsReadOnly(new[]
        {
            QuestPortDefinition.FlowInput(QuestPortIds.In, "In")
        });

        [SerializeField, Tooltip("Quest result when this node is reached. Rewards can only be connected to Completed.")]
        private QuestEndResult result = QuestEndResult.Completed;

        public QuestEndResult Result
        {
            get => result;
            set => result = value;
        }

        public override IReadOnlyList<QuestPortDefinition> Ports =>
            result == QuestEndResult.Completed ? CompletedPorts : FailedPorts;

        public override string GetSummary()
        {
            return result == QuestEndResult.Completed ? "End: Completed" : "End: Failed";
        }
    }
}
