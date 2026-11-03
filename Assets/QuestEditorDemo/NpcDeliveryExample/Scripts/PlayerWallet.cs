namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Золото игрока. Реализует <see cref="ICurrencyService"/>, чтобы задания могли выдавать награды.
    /// </summary>
    public sealed class PlayerWallet : ICurrencyService
    {
        public int Gold { get; private set; }

        public bool AddGold(int amount)
        {
            Gold += amount;
            return true;
        }
    }
}
