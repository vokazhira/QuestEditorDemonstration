using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor.Tests
{
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
