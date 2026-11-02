using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Состояние одного задания у игрока: состояние, текущий узел, прогресс по ObjectiveId и выданные награды.
    /// Обычный C#-объект; ассет задания не изменяет. Меняется только через <see cref="QuestManager"/>.
    /// </summary>
    public sealed class QuestInstance
    {
        private readonly Dictionary<string, int> progress = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> grantedRewardIds = new HashSet<string>(StringComparer.Ordinal);

        internal QuestInstance(QuestGraphAsset graph)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            QuestId = graph.QuestId;
            State = QuestState.Available;
        }

        public string QuestId { get; }

        /// <summary>
        /// Определение задания. Только для чтения.
        /// </summary>
        public QuestGraphAsset Graph { get; }

        public QuestState State { get; internal set; }

        /// <summary>
        /// Задание восстановлено из сохранения, но продолжить его нельзя (например, сохранённого узла больше нет в графе).
        /// Такое задание не реагирует на события, а его запись сохраняется обратно без изменений.
        /// </summary>
        public bool IsFrozen { get; internal set; }

        public string CurrentNodeId { get; internal set; }

        public QuestNodeData CurrentNode => string.IsNullOrEmpty(CurrentNodeId) ? null : Graph.FindNode(CurrentNodeId);

        /// <summary>
        /// Цель, которую задание ждёт сейчас, или null, если задание не активно.
        /// </summary>
        public ObjectiveNodeData CurrentObjective => State == QuestState.Active && !IsFrozen ? CurrentNode as ObjectiveNodeData : null;

        public IReadOnlyDictionary<string, int> Progress => progress;

        public IReadOnlyCollection<string> GrantedRewardIds => grantedRewardIds;

        public int GetProgress(string objectiveId)
        {
            return objectiveId != null && progress.TryGetValue(objectiveId, out var value) ? value : 0;
        }

        public bool IsRewardGranted(string rewardId)
        {
            return rewardId != null && grantedRewardIds.Contains(rewardId);
        }

        internal void SetProgress(string objectiveId, int value)
        {
            progress[objectiveId] = value;
        }

        internal void MarkRewardGranted(string rewardId)
        {
            grantedRewardIds.Add(rewardId);
        }

        internal void ResetProgress()
        {
            progress.Clear();
            grantedRewardIds.Clear();
            CurrentNodeId = null;
            IsFrozen = false;
        }

        internal void ResetToAvailable()
        {
            ResetProgress();
            State = QuestState.Available;
        }

        public override string ToString()
        {
            return $"{QuestId}: {State} @ {CurrentNodeId}";
        }
    }
}
