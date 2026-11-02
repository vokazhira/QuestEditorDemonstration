namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Хранилище прогресса заданий. Реализация по умолчанию — <see cref="FileQuestSaveService"/>.
    /// </summary>
    public interface IQuestSaveService
    {
        /// <summary>
        /// Загружает сохранение. Отсутствие сохранения — не ошибка: возвращается true и пустые данные.
        /// При ошибке возвращает false и описание в <paramref name="error"/>.
        /// </summary>
        bool TryLoad(out QuestSaveData data, out string error);

        void Save(QuestSaveData data);
    }
}
