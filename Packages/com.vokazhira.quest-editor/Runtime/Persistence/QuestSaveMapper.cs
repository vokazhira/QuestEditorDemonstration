using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Преобразует <see cref="QuestInstance"/> в запись сохранения и обратно.
    /// </summary>
    internal static class QuestSaveMapper
    {
        public static QuestInstanceSaveData ToRecord(QuestInstance instance)
        {
            var record = new QuestInstanceSaveData
            {
                questId = instance.QuestId,
                state = instance.State.ToString(),
                currentNodeId = instance.CurrentNodeId
            };

            var objectiveIds = new List<string>(instance.Progress.Keys);
            objectiveIds.Sort(StringComparer.Ordinal);
            foreach (var objectiveId in objectiveIds)
            {
                record.progress.Add(new ObjectiveProgressEntry(objectiveId, instance.Progress[objectiveId]));
            }

            record.grantedRewardIds.AddRange(instance.GrantedRewardIds);
            record.grantedRewardIds.Sort(StringComparer.Ordinal);
            return record;
        }

        /// <summary>
        /// Переносит запись в экземпляр. Возвращает false и причину, если по записи задание нельзя продолжить
        /// (неизвестное состояние, сохранённого узла нет или он не того типа). Данные, которые удалось прочитать,
        /// переносятся в любом случае.
        /// </summary>
        public static bool TryApply(QuestInstance instance, QuestInstanceSaveData record, out string problem)
        {
            instance.ResetToAvailable();
            instance.CurrentNodeId = string.IsNullOrEmpty(record.currentNodeId) ? null : record.currentNodeId;

            if (record.progress != null)
            {
                foreach (var entry in record.progress)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.objectiveId))
                    {
                        instance.SetProgress(entry.objectiveId, Math.Max(0, entry.value));
                    }
                }
            }

            if (record.grantedRewardIds != null)
            {
                foreach (var rewardId in record.grantedRewardIds)
                {
                    if (!string.IsNullOrEmpty(rewardId))
                    {
                        instance.MarkRewardGranted(rewardId);
                    }
                }
            }

            if (!Enum.TryParse(record.state, false, out QuestState state) || !Enum.IsDefined(typeof(QuestState), state))
            {
                problem = $"неизвестное состояние «{record.state}»";
                return false;
            }

            instance.State = state;
            switch (state)
            {
                case QuestState.Active when !(instance.CurrentNode is ObjectiveNodeData):
                    problem = $"сохранённый узел «{record.currentNodeId}» не найден в графе или не является целью";
                    return false;

                case QuestState.Claimable when !(instance.CurrentNode is EndNodeData end && end.Result == QuestEndResult.Completed):
                    problem = $"сохранённый узел «{record.currentNodeId}» не найден в графе или не является End(Completed)";
                    return false;

                default:
                    problem = null;
                    return true;
            }
        }
    }
}
