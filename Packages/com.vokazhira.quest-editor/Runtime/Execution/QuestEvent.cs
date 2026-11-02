namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Событие игры, на которое реагируют цели заданий.
    /// </summary>
    public readonly struct QuestEvent
    {
        public readonly string Type;
        public readonly string TargetId;
        public readonly int Amount;

        public QuestEvent(string type, string targetId, int amount = 1)
        {
            Type = type ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Amount = amount;
        }

        public static QuestEvent EnemyKilled(string enemyId, int amount = 1)
        {
            return new QuestEvent(QuestEventTypes.EnemyKilled, enemyId, amount);
        }

        public static QuestEvent ItemCollected(string itemId, int amount = 1)
        {
            return new QuestEvent(QuestEventTypes.ItemCollected, itemId, amount);
        }

        public static QuestEvent WaveCompleted(string waveId, int amount = 1)
        {
            return new QuestEvent(QuestEventTypes.WaveCompleted, waveId, amount);
        }

        public override string ToString()
        {
            return $"{Type}({TargetId}, {Amount})";
        }
    }
}
