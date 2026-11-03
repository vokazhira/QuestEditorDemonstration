using System.Collections.Generic;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Предметы, которые несёт игрок.
    /// </summary>
    public sealed class PlayerInventory
    {
        private readonly List<string> items = new List<string>();

        public IReadOnlyList<string> Items => items;

        public void Add(string itemId)
        {
            items.Add(itemId);
        }

        public bool Remove(string itemId)
        {
            return items.Remove(itemId);
        }

        public bool Contains(string itemId)
        {
            return items.Contains(itemId);
        }
    }
}
