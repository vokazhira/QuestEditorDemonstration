using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Награды/Опыт", "#00ACC1")]
    public sealed class ExperienceReward : RewardNodeData
    {
        [SerializeField, Min(1), Tooltip("Количество опыта.")]
        private int amount = 1;

        public int Amount
        {
            get => amount;
            set => amount = value;
        }

        public override string GetSummary()
        {
            return $"+{amount} опыта";
        }
    }
}
