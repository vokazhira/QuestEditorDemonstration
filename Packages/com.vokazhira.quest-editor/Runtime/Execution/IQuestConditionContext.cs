namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Данные игры, доступные условиям при вычислении.
    /// </summary>
    public interface IQuestConditionContext
    {
        QuestState GetState(string questId);
    }
}
