using System;
using NUnit.Framework;

namespace Vokazhira.QuestEditor.Tests
{
    public sealed class QuestRewardDispatcherTests
    {
        private sealed class AnyRewardHandler : IRewardHandler<RewardNodeData>
        {
            public int Calls;

            public bool TryGrant(RewardNodeData reward, string questId)
            {
                Calls++;
                return true;
            }
        }

        private sealed class ThrowingGoldHandler : IRewardHandler<GoldReward>
        {
            public bool TryGrant(GoldReward reward, string questId)
            {
                throw new InvalidOperationException("boom");
            }
        }

        [Test]
        public void HandlerForBaseType_HandlesDerivedRewards()
        {
            var dispatcher = new QuestRewardDispatcher();
            var handler = new AnyRewardHandler();
            dispatcher.Register(handler);

            Assert.That(dispatcher.HasHandler(typeof(GoldReward)), Is.True);
            Assert.That(dispatcher.TryGrant(new ExperienceReward { Amount = 1 }, "q", out var error), Is.True, error);
            Assert.That(handler.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ExactTypeHandler_WinsOverBaseTypeHandler()
        {
            var dispatcher = new QuestRewardDispatcher();
            var baseHandler = new AnyRewardHandler();
            var currency = new FakeCurrencyService();
            dispatcher.Register(baseHandler);
            dispatcher.Register(new GoldRewardHandler(currency));

            dispatcher.TryGrant(new GoldReward { Amount = 7 }, "q", out _);

            Assert.That(currency.Gold, Is.EqualTo(7));
            Assert.That(baseHandler.Calls, Is.EqualTo(0));
        }

        [Test]
        public void MissingHandler_ReturnsFalseWithReason()
        {
            var dispatcher = new QuestRewardDispatcher();

            Assert.That(dispatcher.HasHandler(typeof(GoldReward)), Is.False);
            Assert.That(dispatcher.TryGrant(new GoldReward(), "q", out var error), Is.False);
            Assert.That(error, Does.Contain(nameof(GoldReward)));
        }

        [Test]
        public void ThrowingHandler_ReturnsFalseWithReason()
        {
            var dispatcher = new QuestRewardDispatcher();
            dispatcher.Register(new ThrowingGoldHandler());

            Assert.That(dispatcher.TryGrant(new GoldReward(), "q", out var error), Is.False);
            Assert.That(error, Does.Contain("boom"));
        }

        [Test]
        public void Clear_RemovesHandlers()
        {
            var dispatcher = new QuestRewardDispatcher();
            dispatcher.Register(new GoldRewardHandler(new FakeCurrencyService()));

            dispatcher.Clear();

            Assert.That(dispatcher.HasHandler(typeof(GoldReward)), Is.False);
        }

        [Test]
        public void Register_NullHandler_Throws()
        {
            var dispatcher = new QuestRewardDispatcher();

            Assert.Throws<ArgumentNullException>(() => dispatcher.Register<GoldReward>(null));
        }
    }
}
