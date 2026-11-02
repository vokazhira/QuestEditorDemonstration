using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Objectives/Kill Enemies", "#E53935")]
    public sealed class KillObjective : ObjectiveNodeData
    {
        [SerializeField, Tooltip("Enemy type identifier, for example goblin.")]
        private string enemyId = string.Empty;

        [SerializeField, Min(1), Tooltip("How many enemies must be killed.")]
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
                context.AddError(QuestDiagnosticCodes.InvalidValue, "enemyId is empty.");
            }
        }

        public override string GetSummary()
        {
            return $"Kill {QuestObjectiveMatching.Display(enemyId)} ×{requiredCount}";
        }
    }
}
