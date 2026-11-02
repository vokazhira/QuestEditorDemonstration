using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor.Tests
{
    public sealed class QuestNodeInspectorTests
    {
        private QuestGraphAsset graph;
        private SerializedObject serializedGraph;
        private StartNodeData start;
        private KillObjective kill;
        private EndNodeData end;
        private GoldReward gold;

        [SetUp]
        public void SetUp()
        {
            graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
            graph.hideFlags = HideFlags.DontSave;
            start = new StartNodeData();
            kill = new KillObjective { EnemyId = "goblin" };
            kill.SetRequiredCount(10);
            end = new EndNodeData { Result = QuestEndResult.Completed };
            gold = new GoldReward { Amount = 100 };
            graph.AddNode(start);
            graph.AddNode(kill);
            graph.AddNode(end);
            graph.AddNode(gold);
            graph.TryConnect(start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In, out _);
            graph.TryConnect(kill.Id, QuestPortIds.Out, end.Id, QuestPortIds.In, out _);
            graph.TryConnect(gold.Id, QuestPortIds.Reward, end.Id, QuestPortIds.Rewards, out _);
            serializedGraph = new SerializedObject(graph);
            Undo.IncrementCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            serializedGraph.Dispose();
            Undo.ClearUndo(graph);
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void GetEditableProperties_ListsVisibleFieldsOfObjective()
        {
            var names = QuestNodeInspector.GetEditableProperties(serializedGraph, 1).Select(p => p.name).ToArray();

            Assert.That(names, Is.EqualTo(new[] { "objectiveId", "title", "description", "isOptional", "enemyId", "requiredCount" }));
        }

        [Test]
        public void GetEditableProperties_StartNodeHasNoEditableFields()
        {
            Assert.That(QuestNodeInspector.GetEditableProperties(serializedGraph, 0), Is.Empty);
        }

        [Test]
        public void GetEditableProperties_InvalidIndexReturnsEmpty()
        {
            Assert.That(QuestNodeInspector.GetEditableProperties(serializedGraph, 42), Is.Empty);
        }

        [Test]
        public void EditingThroughSerializedProperty_UpdatesModelAndSummary()
        {
            var enemy = QuestNodeInspector.GetEditableProperties(serializedGraph, 1).Single(p => p.name == "enemyId");
            enemy.stringValue = "orc";
            serializedGraph.ApplyModifiedProperties();

            var node = (KillObjective)graph.FindNode(kill.Id);
            Assert.That(node.EnemyId, Is.EqualTo("orc"));
            Assert.That(node.GetSummary(), Is.EqualTo("Убить orc ×10"));
        }

        [Test]
        public void EditingThroughSerializedProperty_CanBeUndone()
        {
            var amount = QuestNodeInspector.GetEditableProperties(serializedGraph, 3).Single(p => p.name == "amount");
            amount.intValue = 250;
            serializedGraph.ApplyModifiedProperties();
            Assert.That(((GoldReward)graph.FindNode(gold.Id)).Amount, Is.EqualTo(250));

            Undo.PerformUndo();

            Assert.That(((GoldReward)graph.FindNode(gold.Id)).Amount, Is.EqualTo(100));
        }

        [Test]
        public void RemoveDanglingEdges_RemovesRewardEdgesWhenEndBecomesFailed()
        {
            end.Result = QuestEndResult.Failed;

            var removed = QuestGraphEditing.RemoveDanglingEdges(graph, end.Id);
            Undo.FlushUndoRecordObjects();

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(graph.HasEdge(gold.Id, QuestPortIds.Reward, end.Id, QuestPortIds.Rewards), Is.False);
            Assert.That(graph.HasEdge(kill.Id, QuestPortIds.Out, end.Id, QuestPortIds.In), Is.True);

            Undo.PerformUndo();
            Assert.That(graph.HasEdge(gold.Id, QuestPortIds.Reward, end.Id, QuestPortIds.Rewards), Is.True);
        }

        [Test]
        public void RemoveDanglingEdges_NothingToRemove_ReturnsZero()
        {
            Assert.That(QuestGraphEditing.RemoveDanglingEdges(graph, end.Id), Is.EqualTo(0));
            Assert.That(graph.Edges.Count, Is.EqualTo(3));
        }
    }
}
