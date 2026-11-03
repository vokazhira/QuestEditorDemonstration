using System;
using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Пользовательская цель «доставить предмет NPC». Объявлена в примере, а не в пакете: редактор находит её
    /// через [QuestNode] и показывает в меню Create Node → Objectives/Deliver Item.
    /// </summary>
    [Serializable]
    [QuestNode("Objectives/Deliver Item", "#7CB342")]
    public sealed class DeliverItemObjective : ObjectiveNodeData
    {
        [SerializeField, Tooltip("Item the player has to deliver, for example letter.")]
        private string itemId = string.Empty;

        [SerializeField, Tooltip("NPC id that has to receive the item, for example blacksmith.")]
        private string recipientId = string.Empty;

        public string ItemId
        {
            get => itemId;
            set => itemId = value;
        }

        public string RecipientId
        {
            get => recipientId;
            set => recipientId = value;
        }

        public override int RequiredCount => 1;

        public override bool TryGetProgress(in QuestEvent e, out int delta)
        {
            return QuestObjectiveMatching.TryMatch(e, DeliveryEvents.ItemDeliveredType, recipientId, false, out delta);
        }

        public override void Validate(QuestValidationContext context)
        {
            base.Validate(context);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                context.AddError(QuestDiagnosticCodes.InvalidValue, "itemId is empty.");
            }

            if (string.IsNullOrWhiteSpace(recipientId))
            {
                context.AddError(QuestDiagnosticCodes.InvalidValue, "recipientId is empty.");
            }
        }

        public override string GetSummary()
        {
            return $"Deliver {QuestObjectiveMatching.Display(itemId)} to {QuestObjectiveMatching.Display(recipientId)}";
        }
    }
}
