using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Условия/Задание выполнено", "#FB8C00")]
    public sealed class QuestCompletedCondition : ConditionNodeData
    {
        [SerializeField, Tooltip("questId задания, которое должно быть выполнено.")]
        private string questId = string.Empty;

        public string QuestId
        {
            get => questId;
            set => questId = value;
        }

        public override bool Evaluate(IQuestConditionContext context)
        {
            return context != null
                && !string.IsNullOrEmpty(questId)
                && context.GetState(questId) == QuestState.Completed;
        }

        public override void Validate(QuestValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                context.AddError(QuestDiagnosticCodes.InvalidValue, "Не задан questId проверяемого задания.");
            }
        }

        public override string GetSummary()
        {
            return string.IsNullOrEmpty(questId) ? "Задание ? выполнено?" : $"Задание «{questId}» выполнено?";
        }
    }
}
