using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Прогресс игрока по заданиям. Сериализуется через JsonUtility. Граф редактора сюда не попадает.
    /// <see cref="orphanedQuests"/> — записи заданий, которых нет в текущей базе; они сохраняются обратно без изменений.
    /// </summary>
    [Serializable]
    public sealed class QuestSaveData
    {
        public const int CurrentVersion = 1;

        public int saveVersion = CurrentVersion;
        public List<QuestInstanceSaveData> quests = new List<QuestInstanceSaveData>();
        public List<QuestInstanceSaveData> orphanedQuests = new List<QuestInstanceSaveData>();

        /// <summary>
        /// Заменяет null-списки пустыми (JsonUtility оставляет null, если поля нет в файле).
        /// </summary>
        public void Normalize()
        {
            quests = quests ?? new List<QuestInstanceSaveData>();
            orphanedQuests = orphanedQuests ?? new List<QuestInstanceSaveData>();
            quests.RemoveAll(q => q == null);
            orphanedQuests.RemoveAll(q => q == null);
            foreach (var quest in quests)
            {
                quest.progress = quest.progress ?? new List<ObjectiveProgressEntry>();
                quest.grantedRewardIds = quest.grantedRewardIds ?? new List<string>();
            }

            foreach (var quest in orphanedQuests)
            {
                quest.progress = quest.progress ?? new List<ObjectiveProgressEntry>();
                quest.grantedRewardIds = quest.grantedRewardIds ?? new List<string>();
            }
        }
    }
}
