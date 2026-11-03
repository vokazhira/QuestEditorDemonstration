namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Собственный тип события игры: предмет передан NPC. Пакет о нём ничего не знает —
    /// его понимает только <see cref="DeliverItemObjective"/>.
    /// </summary>
    public static class DeliveryEvents
    {
        public const string ItemDeliveredType = "ItemDelivered";

        public static QuestEvent ItemDelivered(string recipientId)
        {
            return new QuestEvent(ItemDeliveredType, recipientId);
        }
    }
}
