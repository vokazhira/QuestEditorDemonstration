using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor.Tests
{
    /// <summary>
    /// Узел с входом наград, но не End: для проверки правила «награда подключена не к End(Completed)».
    /// </summary>
    [Serializable]
    internal sealed class RewardSinkTestNode : QuestNodeData
    {
        private static readonly IReadOnlyList<QuestPortDefinition> PortList = Array.AsReadOnly(new[]
        {
            new QuestPortDefinition(QuestPortIds.Rewards, QuestPortDirection.Input, QuestPortKind.Reward, QuestPortCapacity.Multi, "Награды")
        });

        public override IReadOnlyList<QuestPortDefinition> Ports => PortList;
    }

    /// <summary>
    /// Узел, проверка которого бросает исключение.
    /// </summary>
    [Serializable]
    internal sealed class ThrowingValidationTestNode : QuestNodeData
    {
        public override IReadOnlyList<QuestPortDefinition> Ports => Array.Empty<QuestPortDefinition>();

        public override void Validate(QuestValidationContext context)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
