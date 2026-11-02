using System;

namespace Vokazhira.QuestEditor
{
    public sealed class ExperienceRewardHandler : IRewardHandler<ExperienceReward>
    {
        private readonly IExperienceService experienceService;

        public ExperienceRewardHandler(IExperienceService experienceService)
        {
            this.experienceService = experienceService ?? throw new ArgumentNullException(nameof(experienceService));
        }

        public bool TryGrant(ExperienceReward reward, string questId)
        {
            return experienceService.AddExperience(reward.Amount);
        }
    }
}
