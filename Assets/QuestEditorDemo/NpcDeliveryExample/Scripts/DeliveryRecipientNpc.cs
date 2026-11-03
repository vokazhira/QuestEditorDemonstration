using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// NPC, которому нужно принести предмет. Забирает предмет и сообщает заданиям событие «предмет доставлен».
    /// </summary>
    public sealed class DeliveryRecipientNpc : Npc
    {
        [SerializeField, Tooltip("Item this NPC is waiting for.")]
        private string expectedItemId = "letter";

        public override string Prompt => Bootstrap.Inventory.Contains(expectedItemId)
            ? $"Give the {expectedItemId} to {DisplayName}"
            : $"Talk to {DisplayName}";

        public override string Interact()
        {
            if (!Bootstrap.Inventory.Remove(expectedItemId))
            {
                return "Hello, traveller. Busy day at the forge.";
            }

            Bootstrap.Quests.Report(DeliveryEvents.ItemDelivered(NpcId));
            return "A letter from the elder? Thank you! Go back to him, he will reward you.";
        }
    }
}
