using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    [Serializable]
    [QuestNode("Награды/Золото", "#FDD835")]
    public sealed class GoldReward : RewardNodeData
    {
        [SerializeField, Min(1), Tooltip("Количество золота.")]
        private int amount = 1;

        public int Amount
        {
            get => amount;
            set => amount = value;
        }

        public override string GetSummary()
        {
            return $"+{amount} золота";
        }
    }
}
