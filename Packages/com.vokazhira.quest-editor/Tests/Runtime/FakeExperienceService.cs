namespace Vokazhira.QuestEditor.Tests
{
    internal sealed class FakeExperienceService : IExperienceService
    {
        public int Experience;
        public bool Accept = true;

        public bool AddExperience(int amount)
        {
            if (!Accept)
            {
                return false;
            }

            Experience += amount;
            return true;
        }
    }
}
