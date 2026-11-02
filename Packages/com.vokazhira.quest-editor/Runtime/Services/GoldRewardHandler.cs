using System;

namespace Vokazhira.QuestEditor
{
    public sealed class GoldRewardHandler : IRewardHandler<GoldReward>
    {
        private readonly ICurrencyService currencyService;

        public GoldRewardHandler(ICurrencyService currencyService)
        {
            this.currencyService = currencyService ?? throw new ArgumentNullException(nameof(currencyService));
        }

        public bool TryGrant(GoldReward reward, string questId)
        {
            return currencyService.AddGold(reward.Amount);
        }
    }
}
