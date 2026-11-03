using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// NPC, который выдаёт задание вместе с предметом для доставки и выдаёт награду, когда игрок возвращается.
    /// </summary>
    public sealed class QuestGiverNpc : Npc
    {
        [SerializeField, Tooltip("questId of the quest this NPC gives.")]
        private string questId = string.Empty;

        [SerializeField, Tooltip("Item given to the player when the quest starts.")]
        private string itemToGive = "letter";

        public override string Prompt
        {
            get
            {
                switch (Bootstrap.Quests.GetState(questId))
                {
                    case QuestState.Available:
                        return $"Talk to {DisplayName} (quest available)";
                    case QuestState.Claimable:
                        return $"Claim reward from {DisplayName}";
                    default:
                        return $"Talk to {DisplayName}";
                }
            }
        }

        public override string Interact()
        {
            var quests = Bootstrap.Quests;
            switch (quests.GetState(questId))
            {
                case QuestState.Available:
                    if (!quests.Start(questId))
                    {
                        return "I have nothing for you right now.";
                    }

                    Bootstrap.Inventory.Add(itemToGive);
                    return "Please take this letter to the blacksmith. He has been waiting for it.";

                case QuestState.Active:
                    return "Has the blacksmith got my letter yet?";

                case QuestState.Claimable:
                    return quests.Claim(questId)
                        ? "He got it? Thank you! Here is your reward."
                        : "I can't pay you right now, come back later.";

                case QuestState.Completed:
                    return "Thanks again for your help, traveller.";

                default:
                    return "Never mind.";
            }
        }
    }
}
