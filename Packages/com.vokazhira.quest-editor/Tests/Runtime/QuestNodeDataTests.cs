using System;
using System.Linq;
using NUnit.Framework;

namespace Vokazhira.QuestEditor.Tests
{
    public sealed class QuestNodeDataTests
    {
        private sealed class FakeConditionContext : IQuestConditionContext
        {
            public QuestState State;

            public QuestState GetState(string questId)
            {
                return State;
            }
        }

        [Test]
        public void Ports_MatchSpecification()
        {
            AssertPorts(new StartNodeData(), "out");
            AssertPorts(new KillObjective(), "in", "out");
            AssertPorts(new QuestCompletedCondition(), "in", "true", "false");
            AssertPorts(new GoldReward(), "reward");
            AssertPorts(new EndNodeData { Result = QuestEndResult.Completed }, "in", "rewards");
            AssertPorts(new EndNodeData { Result = QuestEndResult.Failed }, "in");
        }

        [Test]
        public void Ports_HaveExpectedKindsDirectionsAndCapacities()
        {
            var startOut = new StartNodeData().FindPort(QuestPortIds.Out);
            Assert.That(startOut.Direction, Is.EqualTo(QuestPortDirection.Output));
            Assert.That(startOut.Kind, Is.EqualTo(QuestPortKind.Flow));
            Assert.That(startOut.Capacity, Is.EqualTo(QuestPortCapacity.Single));

            var objectiveIn = new KillObjective().FindPort(QuestPortIds.In);
            Assert.That(objectiveIn.Direction, Is.EqualTo(QuestPortDirection.Input));
            Assert.That(objectiveIn.Capacity, Is.EqualTo(QuestPortCapacity.Multi));

            var rewardOut = new GoldReward().FindPort(QuestPortIds.Reward);
            Assert.That(rewardOut.Kind, Is.EqualTo(QuestPortKind.Reward));
            Assert.That(rewardOut.Capacity, Is.EqualTo(QuestPortCapacity.Single));

            var endRewards = new EndNodeData().FindPort(QuestPortIds.Rewards);
            Assert.That(endRewards.Direction, Is.EqualTo(QuestPortDirection.Input));
            Assert.That(endRewards.Kind, Is.EqualTo(QuestPortKind.Reward));
            Assert.That(endRewards.Capacity, Is.EqualTo(QuestPortCapacity.Multi));
        }

        [Test]
        public void KillObjective_CountsOnlyMatchingEnemy()
        {
            var kill = new KillObjective { EnemyId = "goblin" };

            Assert.That(kill.TryGetProgress(QuestEvent.EnemyKilled("goblin", 2), out var delta), Is.True);
            Assert.That(delta, Is.EqualTo(2));
            Assert.That(kill.TryGetProgress(QuestEvent.EnemyKilled("orc"), out _), Is.False);
            Assert.That(kill.TryGetProgress(QuestEvent.ItemCollected("goblin"), out _), Is.False);
            Assert.That(kill.TryGetProgress(QuestEvent.EnemyKilled("goblin", 0), out _), Is.False);
        }

        [Test]
        public void KillObjective_WithEmptyEnemyId_MatchesNothing()
        {
            var kill = new KillObjective();

            Assert.That(kill.TryGetProgress(QuestEvent.EnemyKilled("goblin"), out _), Is.False);
        }

        [Test]
        public void CollectObjective_CountsOnlyMatchingItem()
        {
            var collect = new CollectObjective { ItemId = "herb" };

            Assert.That(collect.TryGetProgress(QuestEvent.ItemCollected("herb", 3), out var delta), Is.True);
            Assert.That(delta, Is.EqualTo(3));
            Assert.That(collect.TryGetProgress(QuestEvent.ItemCollected("stone"), out _), Is.False);
        }

        [Test]
        public void CompleteWaveObjective_EmptyWaveIdMatchesAnyWave()
        {
            var anyWave = new CompleteWaveObjective();
            var specificWave = new CompleteWaveObjective { WaveId = "wave-1" };

            Assert.That(anyWave.TryGetProgress(QuestEvent.WaveCompleted("wave-7"), out _), Is.True);
            Assert.That(specificWave.TryGetProgress(QuestEvent.WaveCompleted("wave-1"), out _), Is.True);
            Assert.That(specificWave.TryGetProgress(QuestEvent.WaveCompleted("wave-2"), out _), Is.False);
        }

        [Test]
        public void QuestCompletedCondition_IsTrueOnlyForCompletedQuest()
        {
            var condition = new QuestCompletedCondition { QuestId = "intro" };
            var context = new FakeConditionContext();

            foreach (QuestState state in Enum.GetValues(typeof(QuestState)))
            {
                context.State = state;
                Assert.That(condition.Evaluate(context), Is.EqualTo(state == QuestState.Completed), state.ToString());
            }

            Assert.That(condition.Evaluate(null), Is.False);
        }

        [Test]
        public void GetSummary_DescribesNode()
        {
            var kill = new KillObjective { EnemyId = "goblin" };
            kill.SetRequiredCount(10);

            Assert.That(kill.GetSummary(), Is.EqualTo("Убить goblin ×10"));
            Assert.That(new GoldReward { Amount = 100 }.GetSummary(), Is.EqualTo("+100 золота"));
        }

        [Test]
        public void ConcreteNodeTypes_HaveQuestNodeAttributeWithValidColor()
        {
            var concreteTypes = typeof(QuestNodeData).Assembly.GetTypes()
                .Where(t => typeof(QuestNodeData).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            Assert.That(concreteTypes, Is.Not.Empty);
            foreach (var type in concreteTypes)
            {
                var attribute = (QuestNodeAttribute)Attribute.GetCustomAttribute(type, typeof(QuestNodeAttribute));
                Assert.That(attribute, Is.Not.Null, type.Name);
                Assert.That(attribute.MenuPath, Is.Not.Empty, type.Name);
                Assert.That(attribute.TryGetColor(out _), Is.True, type.Name);
            }
        }

        private static void AssertPorts(QuestNodeData node, params string[] expectedPortIds)
        {
            var actual = node.Ports.Select(p => p.PortId).ToArray();
            Assert.That(actual, Is.EqualTo(expectedPortIds), node.GetType().Name);
        }
    }
}
