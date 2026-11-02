using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Rewards/Gold", "#FDD835")]
    public sealed class GoldReward : RewardNodeData
    {
        [SerializeField, Min(1), Tooltip("Amount of gold.")]
        private int amount = 1;

        public int Amount
        {
            get => amount;
            set => amount = value;
        }

        public override void Validate(QuestValidationContext context)
        {
            base.Validate(context);
            if (amount <= 0)
            {
                context.AddError(QuestDiagnosticCodes.InvalidValue, $"Gold amount must be greater than 0 (currently {amount}).");
            }
        }

        public override string GetSummary()
        {
            return $"+{amount} gold";
        }
    }
}
