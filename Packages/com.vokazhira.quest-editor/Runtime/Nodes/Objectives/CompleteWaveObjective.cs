using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Цели/Пройти волны", "#8E24AA")]
    public sealed class CompleteWaveObjective : ObjectiveNodeData
    {
        [SerializeField, Tooltip("Идентификатор волны. Пусто — засчитывается любая волна.")]
        private string waveId = string.Empty;

        [SerializeField, Min(1), Tooltip("Сколько волн нужно пройти.")]
        private int requiredCount = 1;

        public string WaveId
        {
            get => waveId;
            set => waveId = value;
        }

        public override int RequiredCount => requiredCount;

        public void SetRequiredCount(int value)
        {
            requiredCount = value;
        }

        public override bool TryGetProgress(in QuestEvent e, out int delta)
        {
            return QuestObjectiveMatching.TryMatch(e, QuestEventTypes.WaveCompleted, waveId, true, out delta);
        }

        public override string GetSummary()
        {
            var target = string.IsNullOrEmpty(waveId) ? "любые волны" : $"волну {waveId}";
            return $"Пройти {target} ×{requiredCount}";
        }
    }
}
