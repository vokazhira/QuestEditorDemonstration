namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Выдаёт награду определённого типа. Возвращает false, если выдать не удалось — тогда награду можно
    /// получить повторным <see cref="QuestManager.Claim"/>.
    /// </summary>
    public interface IRewardHandler<in TReward> where TReward : RewardNodeData
    {
        bool TryGrant(TReward reward, string questId);
    }
}
