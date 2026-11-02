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
            QuestPortDefinition.FlowInput(QuestPortIds.In, "Вход"),
            QuestPortDefinition.FlowOutput(QuestPortIds.True, "Да"),
            QuestPortDefinition.FlowOutput(QuestPortIds.False, "Нет")
        });

        public override IReadOnlyList<QuestPortDefinition> Ports => PortList;

        public abstract bool Evaluate(IQuestConditionContext context);
    }
}
