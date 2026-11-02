using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Conditions/Quest Completed", "#FB8C00")]
    public sealed class QuestCompletedCondition : ConditionNodeData
    {
        [SerializeField, Tooltip("questId of the quest that must be completed.")]
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
                context.AddError(QuestDiagnosticCodes.InvalidValue, "The questId of the quest to check is empty.");
            }
        }

        public override string GetSummary()
        {
            return string.IsNullOrEmpty(questId) ? "Quest ? completed?" : $"Quest '{questId}' completed?";
        }
    }
}
