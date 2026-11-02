using System;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Общая проверка «тип события + идентификатор цели» для встроенных целей.
    /// </summary>
    public static class QuestObjectiveMatching
    {
        public static bool TryMatch(in QuestEvent e, string eventType, string targetId, bool emptyTargetMatchesAny, out int delta)
        {
            delta = 0;
            if (e.Amount <= 0 || !string.Equals(e.Type, eventType, StringComparison.Ordinal))
            {
                return false;
            }

            var matchesTarget = string.IsNullOrEmpty(targetId)
                ? emptyTargetMatchesAny
                : string.Equals(e.TargetId, targetId, StringComparison.Ordinal);

            if (!matchesTarget)
            {
                return false;
            }

            delta = e.Amount;
            return true;
        }

        public static string Display(string id)
        {
            return string.IsNullOrEmpty(id) ? "?" : id;
        }
    }
}
