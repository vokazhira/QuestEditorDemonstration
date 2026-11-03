using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Создаёт сервисы игры и <see cref="QuestManager"/> и регистрирует обработчик золота.
    /// NPC, игрок и HUD получают его через ссылку в инспекторе.
    /// </summary>
    public sealed class NpcDeliveryBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("Quests available in this scene.")]
        private QuestDatabase database;

        public QuestManager Quests { get; private set; }
        public PlayerWallet Wallet { get; private set; }
        public PlayerInventory Inventory { get; private set; }

        private void Awake()
        {
            if (database == null)
            {
                Debug.LogError("NpcDeliveryBootstrap: no quest database is assigned.", this);
                enabled = false;
                return;
            }

            Wallet = new PlayerWallet();
            Inventory = new PlayerInventory();
            Quests = new QuestManager(database, null);
            Quests.RegisterRewardHandler(new GoldRewardHandler(Wallet));
        }

        private void OnDestroy()
        {
            Quests?.Dispose();
            Quests = null;
        }
    }
}
