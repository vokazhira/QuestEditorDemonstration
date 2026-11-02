using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Сохранённое состояние одного задания. Состояние хранится строкой (имя <see cref="QuestState"/>),
    /// чтобы порядок значений перечисления не влиял на старые сохранения.
    /// </summary>
    [Serializable]
    public sealed class QuestInstanceSaveData
    {
        public string questId;
        public string state;
        public string currentNodeId;
        public List<ObjectiveProgressEntry> progress = new List<ObjectiveProgressEntry>();
        public List<string> grantedRewardIds = new List<string>();

        public QuestInstanceSaveData Clone()
        {
            var copy = new QuestInstanceSaveData
            {
                questId = questId,
                state = state,
                currentNodeId = currentNodeId,
                grantedRewardIds = grantedRewardIds != null ? new List<string>(grantedRewardIds) : new List<string>()
            };

            if (progress != null)
            {
                foreach (var entry in progress)
                {
                    if (entry != null)
                    {
                        copy.progress.Add(new ObjectiveProgressEntry(entry.objectiveId, entry.value));
                    }
                }
            }

            return copy;
        }
    }
}
