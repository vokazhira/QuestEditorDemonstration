using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Цели/Собрать предметы", "#1E88E5")]
    public sealed class CollectObjective : ObjectiveNodeData
    {
        [SerializeField, Tooltip("Идентификатор предмета, например herb.")]
        private string itemId = string.Empty;

        [SerializeField, Min(1), Tooltip("Сколько предметов нужно собрать.")]
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
                context.AddError(QuestDiagnosticCodes.InvalidValue, "Не задан itemId.");
            }
        }

        public override string GetSummary()
        {
            return $"Собрать {QuestObjectiveMatching.Display(itemId)} ×{requiredCount}";
        }
    }
}
