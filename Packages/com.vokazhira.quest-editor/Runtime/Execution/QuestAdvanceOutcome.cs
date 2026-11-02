namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Чем закончилось продвижение задания по графу.
    /// </summary>
    internal enum QuestAdvanceOutcome
    {
        WaitingForObjective,
        ReachedCompletedEnd,
        ReachedFailedEnd,
        Broken
    }
}
