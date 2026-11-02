using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Описание награды. Не участвует в потоке исполнения: подключается к End(Completed)
    /// и выдаётся при входе в него.
    /// </summary>
    [Serializable]
    public abstract class RewardNodeData : QuestNodeData
    {
        private static readonly IReadOnlyList<QuestPortDefinition> PortList = Array.AsReadOnly(new[]
        {
            new QuestPortDefinition(QuestPortIds.Reward, QuestPortDirection.Output, QuestPortKind.Reward, QuestPortCapacity.Single, "Награда")
        });

        [SerializeField, Tooltip("Стабильный идентификатор награды. По нему отслеживается, что награда уже выдана.")]
        private string rewardId;

        protected RewardNodeData()
        {
            rewardId = Guid.NewGuid().ToString("N");
        }

        public string RewardId
        {
            get => rewardId;
            set => rewardId = value;
        }

        public override IReadOnlyList<QuestPortDefinition> Ports => PortList;
    }
}
