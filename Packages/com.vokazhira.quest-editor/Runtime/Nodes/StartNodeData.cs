using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Поток/Старт", "#43A047")]
    public sealed class StartNodeData : QuestNodeData
    {
        private static readonly IReadOnlyList<QuestPortDefinition> PortList = Array.AsReadOnly(new[]
        {
            QuestPortDefinition.FlowOutput(QuestPortIds.Out, "Далее")
        });

        public override IReadOnlyList<QuestPortDefinition> Ports => PortList;

        public override string GetSummary()
        {
            return "Старт";
        }
    }
}
