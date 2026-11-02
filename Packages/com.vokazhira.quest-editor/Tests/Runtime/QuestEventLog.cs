using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor.Tests
{
    /// <summary>
    /// Записывает события <see cref="QuestManager"/> в порядке их появления.
    /// </summary>
    internal sealed class QuestEventLog
    {
        public readonly List<string> Entries = new List<string>();

        public QuestEventLog(QuestManager manager)
        {
            manager.QuestStarted += q => Entries.Add($"started:{q.QuestId}");
            manager.QuestProgressChanged += (q, o, p) => Entries.Add($"progress:{q.QuestId}:{p}/{o.RequiredCount}");
            manager.QuestClaimable += q => Entries.Add($"claimable:{q.QuestId}");
            manager.QuestCompleted += q => Entries.Add($"completed:{q.QuestId}");
            manager.QuestFailed += q => Entries.Add($"failed:{q.QuestId}");
            manager.QuestRewardGranted += (q, r) => Entries.Add($"granted:{q.QuestId}:{r.GetSummary()}");
            manager.QuestRewardFailed += (q, r, e) => Entries.Add($"reward-failed:{q.QuestId}:{r.GetSummary()}");
        }

        public int Count(string prefix)
        {
            return Entries.FindAll(e => e.StartsWith(prefix, StringComparison.Ordinal)).Count;
        }
    }
}
