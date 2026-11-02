using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor.Tests
{
    public sealed class QuestWorkingCopyTests
    {
        private const string TempFolder = "Assets/QuestEditorTestsTemp";
        private const string AssetPath = TempFolder + "/WorkingCopy.asset";

        private QuestGraphAsset asset;
        private QuestWorkingCopy workingCopy;
        private string startId;
        private string killId;
        private string endId;
        private string goldId;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "QuestEditorTestsTemp");
            }

            var source = ScriptableObject.CreateInstance<QuestGraphAsset>();
            source.QuestId = "forest_cleanup";
            var start = new StartNodeData();
            var kill = new KillObjective { EnemyId = "goblin" };
            kill.SetRequiredCount(10);
            var end = new EndNodeData();
            var gold = new GoldReward { Amount = 100 };
            source.AddNode(start);
            source.AddNode(kill);
            source.AddNode(end);
            source.AddNode(gold);
            source.TryConnect(start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In, out _);
            source.TryConnect(kill.Id, QuestPortIds.Out, end.Id, QuestPortIds.In, out _);
            source.TryConnect(gold.Id, QuestPortIds.Reward, end.Id, QuestPortIds.Rewards, out _);

            AssetDatabase.CreateAsset(source, AssetPath);
            AssetDatabase.SaveAssets();
            asset = AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(AssetPath);

            startId = start.Id;
            killId = kill.Id;
            endId = end.Id;
            goldId = gold.Id;
        }

        [TearDown]
        public void TearDown()
        {
            workingCopy?.Dispose();
            workingCopy = null;

            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void Open_CreatesIndependentInMemoryCopy()
        {
            workingCopy = QuestWorkingCopy.Open(asset);
            var copy = workingCopy.Graph;

            Assert.That(copy, Is.Not.SameAs(asset));
            Assert.That(EditorUtility.IsPersistent(copy), Is.False);
            Assert.That((copy.hideFlags & HideFlags.DontSave) == HideFlags.DontSave, Is.True);
            Assert.That(copy.name, Is.EqualTo(asset.name));
            Assert.That(workingCopy.IsDraft, Is.False);
            Assert.That(workingCopy.AssetGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(AssetPath)));
            Assert.That(workingCopy.RefreshUnsavedState(), Is.False);

            Assert.That(copy.Nodes.Count, Is.EqualTo(asset.Nodes.Count));
            for (var i = 0; i < copy.Nodes.Count; i++)
            {
                Assert.That(copy.Nodes[i], Is.Not.SameAs(asset.Nodes[i]));
                Assert.That(copy.Nodes[i].Id, Is.EqualTo(asset.Nodes[i].Id));
            }
        }

        [Test]
        public void ChangingCopy_DoesNotTouchAssetAndMarksUnsaved()
        {
            workingCopy = QuestWorkingCopy.Open(asset);

            ((KillObjective)workingCopy.Graph.FindNode(killId)).EnemyId = "orc";

            Assert.That(((KillObjective)asset.FindNode(killId)).EnemyId, Is.EqualTo("goblin"));
            Assert.That(workingCopy.RefreshUnsavedState(), Is.True);
        }

        [Test]
        public void RevertingChange_ClearsUnsavedState()
        {
            workingCopy = QuestWorkingCopy.Open(asset);
            var kill = (KillObjective)workingCopy.Graph.FindNode(killId);

            kill.EnemyId = "orc";
            Assert.That(workingCopy.RefreshUnsavedState(), Is.True);

            kill.EnemyId = "goblin";
            Assert.That(workingCopy.RefreshUnsavedState(), Is.False);
        }

        [Test]
        public void Save_CopySerializedTransfersManagedReferenceNodes()
        {
            workingCopy = QuestWorkingCopy.Open(asset);
            var graph = workingCopy.Graph;
            var kill = (KillObjective)graph.FindNode(killId);
            kill.EnemyId = "orc";
            kill.SetRequiredCount(5);
            graph.RemoveNode(goldId);
            var experience = new ExperienceReward { Amount = 25 };
            graph.AddNode(experience);
            Assert.That(graph.TryConnect(experience.Id, QuestPortIds.Reward, endId, QuestPortIds.Rewards, out var reason), Is.True, reason);

            Assert.That(workingCopy.Save(out var error), Is.True, error);

            Assert.That(asset.name, Is.EqualTo("WorkingCopy"));
            Assert.That(asset.hideFlags, Is.EqualTo(HideFlags.None));
            Assert.That(EditorUtility.IsPersistent(asset), Is.True);
            Assert.That(workingCopy.RefreshUnsavedState(), Is.False);

            Assert.That(asset.Nodes.Select(n => n.GetType()), Is.EqualTo(new[]
            {
                typeof(StartNodeData), typeof(KillObjective), typeof(EndNodeData), typeof(ExperienceReward)
            }));
            var savedKill = (KillObjective)asset.FindNode(killId);
            Assert.That(savedKill.EnemyId, Is.EqualTo("orc"));
            Assert.That(savedKill.RequiredCount, Is.EqualTo(5));
            Assert.That(((ExperienceReward)asset.FindNode(experience.Id)).Amount, Is.EqualTo(25));
            Assert.That(asset.FindNode(goldId), Is.Null);
            Assert.That(asset.Edges.Count, Is.EqualTo(3));
            Assert.That(asset.HasEdge(experience.Id, QuestPortIds.Reward, endId, QuestPortIds.Rewards), Is.True);
            Assert.That(asset.HasEdge(startId, QuestPortIds.Out, killId, QuestPortIds.In), Is.True);

            for (var i = 0; i < asset.Nodes.Count; i++)
            {
                Assert.That(asset.Nodes[i], Is.Not.SameAs(graph.Nodes[i]));
            }

            kill.EnemyId = "troll";
            Assert.That(savedKill.EnemyId, Is.EqualTo("orc"));

            var yaml = File.ReadAllText(AssetPath);
            Assert.That(yaml, Does.Contain("class: ExperienceReward, ns: Vokazhira.QuestEditor, asm: Vokazhira.QuestEditor"));
            Assert.That(yaml, Does.Not.Contain("class: GoldReward"));
            Assert.That(yaml, Does.Contain("enemyId: orc"));
            Assert.That(yaml, Does.Contain("m_Name: WorkingCopy"));
        }

        [Test]
        public void SaveAs_CreatesNewAssetAndRebindsCopy()
        {
            const string otherPath = TempFolder + "/Other.asset";
            workingCopy = QuestWorkingCopy.Open(asset);
            ((KillObjective)workingCopy.Graph.FindNode(killId)).EnemyId = "orc";

            Assert.That(workingCopy.SaveAs(otherPath, out var error), Is.True, error);

            var other = AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(otherPath);
            Assert.That(other, Is.Not.Null);
            Assert.That(other.name, Is.EqualTo("Other"));
            Assert.That(((KillObjective)other.FindNode(killId)).EnemyId, Is.EqualTo("orc"));
            Assert.That(((KillObjective)asset.FindNode(killId)).EnemyId, Is.EqualTo("goblin"));
            Assert.That(workingCopy.AssetGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(otherPath)));
            Assert.That(workingCopy.RefreshUnsavedState(), Is.False);
        }

        [Test]
        public void SaveAs_RefusesToOverwriteAssetOfAnotherType()
        {
            const string otherTypePath = TempFolder + "/NotAQuest.asset";
            AssetDatabase.CreateAsset(new Texture2D(2, 2), otherTypePath);
            workingCopy = QuestWorkingCopy.Open(asset);

            Assert.That(workingCopy.SaveAs(otherTypePath, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(otherTypePath), Is.InstanceOf<Texture2D>());
        }

        [Test]
        public void Draft_IsUnsavedUntilSavedAs()
        {
            const string draftPath = TempFolder + "/Draft.asset";
            workingCopy = QuestWorkingCopy.CreateDraft("draft_quest");

            Assert.That(workingCopy.IsDraft, Is.True);
            Assert.That(workingCopy.RefreshUnsavedState(), Is.True);
            Assert.That(workingCopy.Graph.QuestId, Is.EqualTo("draft_quest"));
            Assert.That(workingCopy.Graph.Nodes.Count, Is.EqualTo(2));
            Assert.That(workingCopy.Save(out _), Is.False);

            Assert.That(workingCopy.SaveAs(draftPath, out var error), Is.True, error);

            Assert.That(workingCopy.IsDraft, Is.False);
            Assert.That(workingCopy.RefreshUnsavedState(), Is.False);
            var saved = AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(draftPath);
            Assert.That(saved.QuestId, Is.EqualTo("draft_quest"));
            Assert.That(saved.name, Is.EqualTo("Draft"));
            Assert.That(saved.Nodes.Count, Is.EqualTo(2));
        }

        [Test]
        public void DetachFromAsset_TurnsCopyIntoUnsavedDraft()
        {
            workingCopy = QuestWorkingCopy.Open(asset);

            workingCopy.DetachFromAsset();

            Assert.That(workingCopy.IsDraft, Is.True);
            Assert.That(workingCopy.HasUnsavedChanges, Is.True);
            Assert.That(workingCopy.IsAlive, Is.True);
        }

        [Test]
        public void Dispose_DestroysInMemoryCopyButNotAsset()
        {
            workingCopy = QuestWorkingCopy.Open(asset);
            var copy = workingCopy.Graph;

            workingCopy.Dispose();

            Assert.That(copy == null, Is.True);
            Assert.That(workingCopy.IsAlive, Is.False);
            Assert.That(asset != null, Is.True);
        }
    }
}
