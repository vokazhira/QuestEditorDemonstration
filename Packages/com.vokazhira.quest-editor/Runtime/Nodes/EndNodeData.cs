using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Поток/Финал", "#757575")]
    public sealed class EndNodeData : QuestNodeData
    {
        private static readonly IReadOnlyList<QuestPortDefinition> CompletedPorts = Array.AsReadOnly(new[]
        {
            QuestPortDefinition.FlowInput(QuestPortIds.In, "Вход"),
            new QuestPortDefinition(QuestPortIds.Rewards, QuestPortDirection.Input, QuestPortKind.Reward, QuestPortCapacity.Multi, "Награды")
        });

        private static readonly IReadOnlyList<QuestPortDefinition> FailedPorts = Array.AsReadOnly(new[]
        {
            QuestPortDefinition.FlowInput(QuestPortIds.In, "Вход")
        });

        [SerializeField, Tooltip("Результат задания при входе в этот узел. Награды подключаются только к Completed.")]
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
            return result == QuestEndResult.Completed ? "Финал: выполнено" : "Финал: провал";
        }
    }
}
