using System;
using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Находит обработчик для типа награды (сначала точный тип, затем базовые) и выдаёт награду.
    /// </summary>
    internal sealed class QuestRewardDispatcher
    {
        private readonly Dictionary<Type, Func<RewardNodeData, string, bool>> handlers = new Dictionary<Type, Func<RewardNodeData, string, bool>>();

        public void Register<TReward>(IRewardHandler<TReward> handler) where TReward : RewardNodeData
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            handlers[typeof(TReward)] = (reward, questId) => handler.TryGrant((TReward)reward, questId);
        }

        public bool HasHandler(Type rewardType)
        {
            return FindHandler(rewardType) != null;
        }

        public bool TryGrant(RewardNodeData reward, string questId, out string error)
        {
            var handler = FindHandler(reward.GetType());
            if (handler == null)
            {
                error = $"No handler is registered for reward type {reward.GetType().Name}.";
                return false;
            }

            try
            {
                if (handler(reward, questId))
                {
                    error = null;
                    return true;
                }

                error = $"The handler for reward {reward.GetType().Name} returned false.";
                return false;
            }
            catch (Exception exception)
            {
                error = $"The handler for reward {reward.GetType().Name} threw an exception: {exception.Message}";
                return false;
            }
        }

        public void Clear()
        {
            handlers.Clear();
        }

        private Func<RewardNodeData, string, bool> FindHandler(Type rewardType)
        {
            for (var type = rewardType; type != null && typeof(RewardNodeData).IsAssignableFrom(type); type = type.BaseType)
            {
                if (handlers.TryGetValue(type, out var handler))
                {
                    return handler;
                }
            }

            return null;
        }
    }
}
