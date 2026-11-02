using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Цели/Убить врагов", "#E53935")]
    public sealed class KillObjective : ObjectiveNodeData
    {
        [SerializeField, Tooltip("Идентификатор типа врага, например goblin.")]
        private string enemyId = string.Empty;

        [SerializeField, Min(1), Tooltip("Сколько врагов нужно убить.")]
        private int requiredCount = 1;

        public string EnemyId
        {
            get => enemyId;
            set => enemyId = value;
        }

        public override int RequiredCount => requiredCount;

        public void SetRequiredCount(int value)
        {
            requiredCount = value;
        }

        public override bool TryGetProgress(in QuestEvent e, out int delta)
        {
            return QuestObjectiveMatching.TryMatch(e, QuestEventTypes.EnemyKilled, enemyId, false, out delta);
        }

        public override void Validate(QuestValidationContext context)
        {
            base.Validate(context);
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                context.AddError(QuestDiagnosticCodes.InvalidValue, "Не задан enemyId.");
            }
        }

        public override string GetSummary()
        {
            return $"Убить {QuestObjectiveMatching.Display(enemyId)} ×{requiredCount}";
        }
    }
}
