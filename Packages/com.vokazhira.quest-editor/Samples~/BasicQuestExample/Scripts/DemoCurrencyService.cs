namespace Vokazhira.QuestEditor.Demo
{
    /// <summary>
    /// Учебная реализация валюты: просто хранит количество золота.
    /// </summary>
    public sealed class DemoCurrencyService : ICurrencyService
    {
        public int Gold { get; private set; }

        public bool AddGold(int amount)
        {
            Gold += amount;
            return true;
        }
    }
}
