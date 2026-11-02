using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Мгновенная проверка с ветвлением по выходам True и False.
    /// </summary>
    [Serializable]
    public abstract class ConditionNodeData : QuestNodeData
    {
        private static readonly IReadOnlyList<QuestPortDefinition> PortList = Array.AsReadOnly(new[]
        {
            QuestPortDefinition.FlowInput(QuestPortIds.In, "In"),
            QuestPortDefinition.FlowOutput(QuestPortIds.True, "True"),
            QuestPortDefinition.FlowOutput(QuestPortIds.False, "False")
        });

        public override IReadOnlyList<QuestPortDefinition> Ports => PortList;

        public abstract bool Evaluate(IQuestConditionContext context);
    }
}
