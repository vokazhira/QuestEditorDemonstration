using System;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Прогресс одной цели в сохранении.
    /// </summary>
    [Serializable]
    public sealed class ObjectiveProgressEntry
    {
        public string objectiveId;
        public int value;

        public ObjectiveProgressEntry()
        {
        }

        public ObjectiveProgressEntry(string objectiveId, int value)
        {
            this.objectiveId = objectiveId;
            this.value = value;
        }
    }
}
