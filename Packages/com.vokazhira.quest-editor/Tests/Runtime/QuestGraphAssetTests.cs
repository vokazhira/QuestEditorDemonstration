using System;
using NUnit.Framework;
using UnityEngine;

namespace Vokazhira.QuestEditor.Tests
{
    public sealed class QuestGraphAssetTests
    {
        private QuestGraphAsset graph;

        [SetUp]
        public void SetUp()
        {
            graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }

        [Test]
        public void NewGraph_IsEmptyWithCurrentFormatVersion()
        {
            Assert.That(graph.Nodes, Is.Empty);
            Assert.That(graph.Edges, Is.Empty);
            Assert.That(graph.FormatVersion, Is.EqualTo(QuestGraphAsset.CurrentFormatVersion));
            Assert.That(graph.RewardMode, Is.EqualTo(QuestRewardMode.AutoGrant));
            Assert.That(graph.QuestId, Is.Empty);
        }

        [Test]
        public void NewNodes_HaveUniqueNonEmptyIds()
        {
            var a = new KillObjective();
            var b = new KillObjective();

            Assert.That(a.Id, Is.Not.Empty);
            Assert.That(b.Id, Is.Not.Empty);
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
            Assert.That(a.ObjectiveId, Is.Not.EqualTo(b.ObjectiveId));
        }

        [Test]
        public void AddNode_NodeCanBeFoundById()
        {
            var start = new StartNodeData();
            var end = new EndNodeData();

            graph.AddNode(start);
            graph.AddNode(end);

            Assert.That(graph.Nodes.Count, Is.EqualTo(2));
            Assert.That(graph.FindNode(start.Id), Is.SameAs(start));
            Assert.That(graph.FindNode(end.Id), Is.SameAs(end));
        }

        [Test]
        public void AddNode_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => graph.AddNode(null));
        }

        [Test]
        public void AddNode_SameNodeTwice_Throws()
        {
            var start = new StartNodeData();
            graph.AddNode(start);

            Assert.Throws<ArgumentException>(() => graph.AddNode(start));
            Assert.That(graph.Nodes.Count, Is.EqualTo(1));
        }

        [Test]
        public void FindNode_UnknownOrEmptyId_ReturnsNull()
        {
            graph.AddNode(new StartNodeData());

            Assert.That(graph.FindNode("missing"), Is.Null);
            Assert.That(graph.FindNode(string.Empty), Is.Null);
            Assert.That(graph.FindNode(null), Is.Null);
        }

        [Test]
        public void FindNode_AfterRemoval_ReturnsNullAndKeepsOtherNodes()
        {
            var a = new KillObjective();
            var b = new CollectObjective();
            var c = new EndNodeData();
            graph.AddNode(a);
            graph.AddNode(b);
            graph.AddNode(c);
            Assert.That(graph.FindNode(c.Id), Is.SameAs(c));

            Assert.That(graph.RemoveNode(a.Id), Is.True);

            Assert.That(graph.FindNode(a.Id), Is.Null);
            Assert.That(graph.FindNode(b.Id), Is.SameAs(b));
            Assert.That(graph.FindNode(c.Id), Is.SameAs(c));
        }

        [Test]
        public void RemoveNode_UnknownId_ReturnsFalse()
        {
            Assert.That(graph.RemoveNode("missing"), Is.False);
        }

        [Test]
        public void TryConnect_ValidPorts_AddsEdge()
        {
            var start = new StartNodeData();
            var kill = new KillObjective();
            graph.AddNode(start);
            graph.AddNode(kill);

            var connected = graph.TryConnect(start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In, out var reason);

            Assert.That(connected, Is.True, reason);
            Assert.That(reason, Is.Null);
            Assert.That(graph.Edges.Count, Is.EqualTo(1));
            Assert.That(graph.HasEdge(start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In), Is.True);
        }

        [Test]
        public void TryConnect_InvalidPorts_DoesNotAddEdgeAndGivesReason()
        {
            var start = new StartNodeData();
            var kill = new KillObjective();
            graph.AddNode(start);
            graph.AddNode(kill);

            var connected = graph.TryConnect(kill.Id, QuestPortIds.In, start.Id, QuestPortIds.Out, out var reason);

            Assert.That(connected, Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(graph.Edges, Is.Empty);
        }

        [Test]
        public void Disconnect_RemovesOnlyMatchingEdge()
        {
            var condition = new QuestCompletedCondition();
            var completed = new EndNodeData { Result = QuestEndResult.Completed };
            var failed = new EndNodeData { Result = QuestEndResult.Failed };
            graph.AddNode(condition);
            graph.AddNode(completed);
            graph.AddNode(failed);
            Assert.That(graph.TryConnect(condition.Id, QuestPortIds.True, completed.Id, QuestPortIds.In, out _), Is.True);
            Assert.That(graph.TryConnect(condition.Id, QuestPortIds.False, failed.Id, QuestPortIds.In, out _), Is.True);

            Assert.That(graph.Disconnect(condition.Id, QuestPortIds.True, completed.Id, QuestPortIds.In), Is.True);

            Assert.That(graph.Edges.Count, Is.EqualTo(1));
            Assert.That(graph.HasEdge(condition.Id, QuestPortIds.False, failed.Id, QuestPortIds.In), Is.True);
            Assert.That(graph.Disconnect(condition.Id, QuestPortIds.True, completed.Id, QuestPortIds.In), Is.False);
        }

        [Test]
        public void RemoveNode_AlsoRemovesItsEdges()
        {
            var start = new StartNodeData();
            var kill = new KillObjective();
            var end = new EndNodeData();
            var gold = new GoldReward();
            graph.AddNode(start);
            graph.AddNode(kill);
            graph.AddNode(end);
            graph.AddNode(gold);
            Assert.That(graph.TryConnect(start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In, out _), Is.True);
            Assert.That(graph.TryConnect(kill.Id, QuestPortIds.Out, end.Id, QuestPortIds.In, out _), Is.True);
            Assert.That(graph.TryConnect(gold.Id, QuestPortIds.Reward, end.Id, QuestPortIds.Rewards, out _), Is.True);

            graph.RemoveNode(kill.Id);

            Assert.That(graph.Edges.Count, Is.EqualTo(1));
            Assert.That(graph.HasEdge(gold.Id, QuestPortIds.Reward, end.Id, QuestPortIds.Rewards), Is.True);
        }

        [Test]
        public void CountEdges_CountsByNodeAndPort()
        {
            var a = new KillObjective();
            var b = new CollectObjective();
            var end = new EndNodeData();
            graph.AddNode(a);
            graph.AddNode(b);
            graph.AddNode(end);
            graph.TryConnect(a.Id, QuestPortIds.Out, end.Id, QuestPortIds.In, out _);
            graph.TryConnect(b.Id, QuestPortIds.Out, end.Id, QuestPortIds.In, out _);

            Assert.That(graph.CountEdgesTo(end.Id, QuestPortIds.In), Is.EqualTo(2));
            Assert.That(graph.CountEdgesFrom(a.Id, QuestPortIds.Out), Is.EqualTo(1));
            Assert.That(graph.CountEdgesTo(end.Id, QuestPortIds.Rewards), Is.EqualTo(0));
        }
    }
}
