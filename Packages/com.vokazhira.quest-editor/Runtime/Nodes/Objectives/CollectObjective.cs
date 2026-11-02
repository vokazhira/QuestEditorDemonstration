using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Objectives/Collect Items", "#1E88E5")]
    public sealed class CollectObjective : ObjectiveNodeData
    {
        [SerializeField, Tooltip("Item identifier, for example herb.")]
        private string itemId = string.Empty;

        [SerializeField, Min(1), Tooltip("How many items must be collected.")]
        private int requiredCount = 1;

        public string ItemId
        {
            get => itemId;
            set => itemId = value;
        }

        public override int RequiredCount => requiredCount;

        public void SetRequiredCount(int value)
        {
            requiredCount = value;
        }

        public override bool TryGetProgress(in QuestEvent e, out int delta)
        {
            return QuestObjectiveMatching.TryMatch(e, QuestEventTypes.ItemCollected, itemId, false, out delta);
        }

        public override void Validate(QuestValidationContext context)
        {
            base.Validate(context);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                context.AddError(QuestDiagnosticCodes.InvalidValue, "itemId is empty.");
            }
        }

        public override string GetSummary()
        {
            return $"Collect {QuestObjectiveMatching.Display(itemId)} ×{requiredCount}";
        }
    }
}
