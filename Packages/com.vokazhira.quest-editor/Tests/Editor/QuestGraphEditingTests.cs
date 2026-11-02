using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor.Tests
{
    public sealed class QuestGraphEditingTests
    {
        private QuestGraphAsset graph;

        [SetUp]
        public void SetUp()
        {
            graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
            graph.hideFlags = HideFlags.DontSave;
            Undo.IncrementCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearUndo(graph);
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void AddNode_CanBeUndoneAndRedone()
        {
            var node = new KillObjective { EnemyId = "goblin" };

            QuestGraphEditing.AddNode(graph, node);
            Undo.FlushUndoRecordObjects();
            Assert.That(graph.Nodes.Count, Is.EqualTo(1));

            Undo.PerformUndo();
            Assert.That(graph.Nodes.Count, Is.EqualTo(0));
            Assert.That(graph.FindNode(node.Id), Is.Null);

            Undo.PerformRedo();
            Assert.That(graph.Nodes.Count, Is.EqualTo(1));
            var restored = graph.FindNode(node.Id) as KillObjective;
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.EnemyId, Is.EqualTo("goblin"));
        }

        [Test]
        public void MoveNodes_CanBeUndone()
        {
            var node = new StartNodeData { Position = new Vector2(10, 10) };
            graph.AddNode(node);
            Undo.IncrementCurrentGroup();

            var moved = QuestGraphEditing.MoveNodes(graph, new[] { new KeyValuePair<string, Vector2>(node.Id, new Vector2(300, 150)) });
            Undo.FlushUndoRecordObjects();

            Assert.That(moved, Is.True);
            Assert.That(graph.FindNode(node.Id).Position, Is.EqualTo(new Vector2(300, 150)));

            Undo.PerformUndo();
            Assert.That(graph.FindNode(node.Id).Position, Is.EqualTo(new Vector2(10, 10)));
        }

        [Test]
        public void MoveNodes_SamePositionOrUnknownNode_ReportsNoChange()
        {
            var node = new StartNodeData { Position = new Vector2(10, 10) };
            graph.AddNode(node);

            var moved = QuestGraphEditing.MoveNodes(graph, new[]
            {
                new KeyValuePair<string, Vector2>(node.Id, new Vector2(10, 10)),
                new KeyValuePair<string, Vector2>("missing", new Vector2(1, 1))
            });

            Assert.That(moved, Is.False);
        }

        [Test]
        public void RemoveNodes_RemovesEdgesAndCanBeUndone()
        {
            var start = new StartNodeData();
            var kill = new KillObjective();
            var end = new EndNodeData();
            graph.AddNode(start);
            graph.AddNode(kill);
            graph.AddNode(end);
            graph.TryConnect(start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In, out _);
            graph.TryConnect(kill.Id, QuestPortIds.Out, end.Id, QuestPortIds.In, out _);
            Undo.IncrementCurrentGroup();

            var removed = QuestGraphEditing.RemoveNodes(graph, new[] { kill.Id });
            Undo.FlushUndoRecordObjects();

            Assert.That(removed, Is.True);
            Assert.That(graph.Nodes.Count, Is.EqualTo(2));
            Assert.That(graph.Edges, Is.Empty);

            Undo.PerformUndo();
            Assert.That(graph.Nodes.Count, Is.EqualTo(3));
            Assert.That(graph.Edges.Count, Is.EqualTo(2));
            Assert.That(graph.FindNode(kill.Id), Is.InstanceOf<KillObjective>());
        }
    }
}
