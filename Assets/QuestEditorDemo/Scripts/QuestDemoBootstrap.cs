using UnityEngine;

namespace Vokazhira.QuestEditor.Demo
{
    /// <summary>
    /// Точка входа демо: создаёт сервисы игры и <see cref="QuestManager"/>, регистрирует обработчики наград
    /// и загружает сохранённый прогресс. Использует только публичный API пакета.
    /// </summary>
    public sealed class QuestDemoBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("База заданий, доступных в демо.")]
        private QuestDatabase database;

        [SerializeField, Tooltip("Файл сохранения относительно Application.persistentDataPath.")]
        private string saveFileName = "QuestEditorDemo/progress.json";

        public QuestManager Manager { get; private set; }
        public DemoCurrencyService Currency { get; private set; }
        public DemoExperienceService Experience { get; private set; }
        public FileQuestSaveService SaveService { get; private set; }

        private void Awake()
        {
            if (database == null)
            {
                Debug.LogError("QuestDemoBootstrap: не назначена база заданий.", this);
                enabled = false;
                return;
            }

            Currency = new DemoCurrencyService();
            Experience = new DemoExperienceService();
            SaveService = new FileQuestSaveService(saveFileName);

            Manager = new QuestManager(database, SaveService);
            Manager.RegisterRewardHandler(new GoldRewardHandler(Currency));
            Manager.RegisterRewardHandler(new ExperienceRewardHandler(Experience));
        }

        private void Start()
        {
            Manager?.Load();
        }

        private void OnDestroy()
        {
            Manager?.Dispose();
            Manager = null;
        }
    }
}
