using NUnit.Framework;
using UnityEngine;

namespace Vokazhira.QuestEditor.Tests
{
    public sealed class QuestPortRulesTests
    {
        private QuestGraphAsset graph;
        private StartNodeData start;
        private KillObjective kill;
        private CollectObjective collect;
        private QuestCompletedCondition condition;
        private EndNodeData completed;
        private EndNodeData failed;
        private GoldReward gold;

        [SetUp]
        public void SetUp()
        {
            graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
            start = Add(new StartNodeData());
            kill = Add(new KillObjective());
            collect = Add(new CollectObjective());
            condition = Add(new QuestCompletedCondition());
            completed = Add(new EndNodeData { Result = QuestEndResult.Completed });
            failed = Add(new EndNodeData { Result = QuestEndResult.Failed });
            gold = Add(new GoldReward());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void FlowOutputToFlowInput_IsAllowed()
        {
            AssertAllowed(start, QuestPortIds.Out, kill, QuestPortIds.In);
            AssertAllowed(kill, QuestPortIds.Out, condition, QuestPortIds.In);
            AssertAllowed(condition, QuestPortIds.True, completed, QuestPortIds.In);
            AssertAllowed(condition, QuestPortIds.False, failed, QuestPortIds.In);
        }

        [Test]
        public void RewardToCompletedEnd_IsAllowed()
        {
            AssertAllowed(gold, QuestPortIds.Reward, completed, QuestPortIds.Rewards);
        }

        [Test]
        public void InputToOutput_IsRejected()
        {
            AssertRejected(kill, QuestPortIds.In, start, QuestPortIds.Out);
        }

        [Test]
        public void OutputToOutput_IsRejected()
        {
            AssertRejected(start, QuestPortIds.Out, kill, QuestPortIds.Out);
        }

        [Test]
        public void FlowToReward_IsRejected()
        {
            AssertRejected(kill, QuestPortIds.Out, completed, QuestPortIds.Rewards);
        }

        [Test]
        public void RewardToFlow_IsRejected()
        {
            AssertRejected(gold, QuestPortIds.Reward, completed, QuestPortIds.In);
        }

        [Test]
        public void RewardToFailedEnd_IsRejectedBecausePortDoesNotExist()
        {
            Assert.That(failed.FindPort(QuestPortIds.Rewards), Is.Null);
            AssertRejected(gold, QuestPortIds.Reward, failed, QuestPortIds.Rewards);
        }

        [Test]
        public void NodeToItself_IsRejected()
        {
            AssertRejected(kill, QuestPortIds.Out, kill, QuestPortIds.In);
        }

        [Test]
        public void UnknownNodeOrPort_IsRejected()
        {
            Assert.That(QuestPortRules.CanConnect(graph, "missing", QuestPortIds.Out, kill.Id, QuestPortIds.In, out _), Is.False);
            Assert.That(QuestPortRules.CanConnect(graph, start.Id, "missing", kill.Id, QuestPortIds.In, out _), Is.False);
            Assert.That(QuestPortRules.CanConnect(null, start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In, out _), Is.False);
        }

        [Test]
        public void DuplicateEdge_IsRejected()
        {
            Connect(kill, QuestPortIds.Out, completed, QuestPortIds.In);

            AssertRejected(kill, QuestPortIds.Out, completed, QuestPortIds.In);
        }

        [Test]
        public void SingleFlowOutput_SecondEdgeIsRejected()
        {
            Connect(start, QuestPortIds.Out, kill, QuestPortIds.In);

            AssertRejected(start, QuestPortIds.Out, collect, QuestPortIds.In);
        }

        [Test]
        public void SingleRewardOutput_SecondEdgeIsRejected()
        {
            var secondCompleted = Add(new EndNodeData { Result = QuestEndResult.Completed });
            Connect(gold, QuestPortIds.Reward, completed, QuestPortIds.Rewards);

            AssertRejected(gold, QuestPortIds.Reward, secondCompleted, QuestPortIds.Rewards);
        }

        [Test]
        public void MultiFlowInput_AcceptsSeveralEdges()
        {
            Connect(kill, QuestPortIds.Out, completed, QuestPortIds.In);

            AssertAllowed(collect, QuestPortIds.Out, completed, QuestPortIds.In);
        }

        [Test]
        public void MultiRewardInput_AcceptsSeveralRewards()
        {
            var experience = Add(new ExperienceReward());
            Connect(gold, QuestPortIds.Reward, completed, QuestPortIds.Rewards);

            AssertAllowed(experience, QuestPortIds.Reward, completed, QuestPortIds.Rewards);
        }

        [Test]
        public void PairCheck_DoesNotLookAtExistingEdges()
        {
            Connect(start, QuestPortIds.Out, kill, QuestPortIds.In);

            var allowed = QuestPortRules.CanConnect(
                start, start.FindPort(QuestPortIds.Out),
                collect, collect.FindPort(QuestPortIds.In),
                out var reason);

            Assert.That(allowed, Is.True, reason);
        }

        private T Add<T>(T node) where T : QuestNodeData
        {
            graph.AddNode(node);
            return node;
        }

        private void Connect(QuestNodeData from, string fromPort, QuestNodeData to, string toPort)
        {
            Assert.That(graph.TryConnect(from.Id, fromPort, to.Id, toPort, out var reason), Is.True, reason);
        }

        private void AssertAllowed(QuestNodeData from, string fromPort, QuestNodeData to, string toPort)
        {
            var allowed = QuestPortRules.CanConnect(graph, from.Id, fromPort, to.Id, toPort, out var reason);
            Assert.That(allowed, Is.True, reason);
            Assert.That(reason, Is.Null);
        }

        private void AssertRejected(QuestNodeData from, string fromPort, QuestNodeData to, string toPort)
        {
            var allowed = QuestPortRules.CanConnect(graph, from.Id, fromPort, to.Id, toPort, out var reason);
            Assert.That(allowed, Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }
    }
}
