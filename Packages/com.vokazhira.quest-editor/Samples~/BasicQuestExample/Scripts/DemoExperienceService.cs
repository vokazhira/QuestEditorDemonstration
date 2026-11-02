namespace Vokazhira.QuestEditor.Demo
{
    /// <summary>
    /// Учебная реализация опыта: просто хранит количество опыта.
    /// </summary>
    public sealed class DemoExperienceService : IExperienceService
    {
        public int Experience { get; private set; }

        public bool AddExperience(int amount)
        {
            Experience += amount;
            return true;
        }
    }
}
