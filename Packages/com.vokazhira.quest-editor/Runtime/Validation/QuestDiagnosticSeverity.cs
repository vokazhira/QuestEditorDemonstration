namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Уровень диагностики. Задание с хотя бы одной ошибкой (Error) нельзя запустить в игре.
    /// </summary>
    public enum QuestDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }
}
