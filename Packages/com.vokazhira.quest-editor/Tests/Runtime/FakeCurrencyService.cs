namespace Vokazhira.QuestEditor.Tests
{
    internal sealed class FakeCurrencyService : ICurrencyService
    {
        public int Gold;
        public bool Accept = true;

        public bool AddGold(int amount)
        {
            if (!Accept)
            {
                return false;
            }

            Gold += amount;
            return true;
        }
    }
}
