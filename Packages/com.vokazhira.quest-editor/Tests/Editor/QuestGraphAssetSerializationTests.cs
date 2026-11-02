using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor.Tests
{
    public sealed class QuestGraphAssetSerializationTests
    {
        private const string TempAssetFolder = "Assets/QuestEditorTestsTemp";
        private const string TempFilePath = "Temp/QuestEditorTests/QuestGraphRoundTrip.asset";

        private QuestGraphAsset source;
        private StartNodeData start;
        private KillObjective kill;
        private EndNodeData end;
        private GoldReward gold;

        [SetUp]
        public void SetUp()
        {
            source = ScriptableObject.CreateInstance<QuestGraphAsset>();
            source.QuestId = "forest_cleanup";
            source.DisplayName = "Зачистка леса";
            source.Description = "Убить гоблинов";
            source.RewardMode = QuestRewardMode.ManualClaim;

            start = new StartNodeData { Position = new Vector2(10, 20) };
            kill = new KillObjective { EnemyId = "goblin", Title = "Гоблины", Position = new Vector2(200, 20) };
            kill.SetRequiredCount(10);
            end = new EndNodeData { Result = QuestEndResult.Completed, Position = new Vector2(400, 20) };
            gold = new GoldReward { Amount = 100, Position = new Vector2(200, 200) };

            source.AddNode(start);
            source.AddNode(kill);
            source.AddNode(end);
            source.AddNode(gold);
            Assert.That(source.TryConnect(start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In, out _), Is.True);
            Assert.That(source.TryConnect(kill.Id, QuestPortIds.Out, end.Id, QuestPortIds.In, out _), Is.True);
            Assert.That(source.TryConnect(gold.Id, QuestPortIds.Reward, end.Id, QuestPortIds.Rewards, out _), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            if (source != null && !EditorUtility.IsPersistent(source))
            {
                Object.DestroyImmediate(source);
            }

            if (AssetDatabase.IsValidFolder(TempAssetFolder))
            {
                AssetDatabase.DeleteAsset(TempAssetFolder);
            }

            if (File.Exists(TempFilePath))
            {
                File.Delete(TempFilePath);
            }
        }

        [Test]
        public void SerializedFileRoundTrip_RestoresNodesEdgesAndSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TempFilePath));
            InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { source }, TempFilePath, true);

            var loaded = InternalEditorUtility.LoadSerializedFileAndForget(TempFilePath)
                .OfType<QuestGraphAsset>()
                .SingleOrDefault();

            try
            {
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded, Is.Not.SameAs(source));
                AssertSameContent(loaded);
            }
            finally
            {
                if (loaded != null)
                {
                    Object.DestroyImmediate(loaded);
                }
            }
        }

        [Test]
        public void AssetDatabaseSave_WritesManagedReferencesAndLoadsBack()
        {
            AssetDatabase.CreateFolder("Assets", "QuestEditorTestsTemp");
            var path = TempAssetFolder + "/QuestGraphRoundTrip.asset";
            AssetDatabase.CreateAsset(source, path);
            AssetDatabase.SaveAssets();

            var yaml = File.ReadAllText(path);
            Assert.That(yaml, Does.Contain("class: KillObjective, ns: Vokazhira.QuestEditor, asm: Vokazhira.QuestEditor"));
            Assert.That(yaml, Does.Contain("class: GoldReward, ns: Vokazhira.QuestEditor, asm: Vokazhira.QuestEditor"));
            Assert.That(yaml, Does.Contain("enemyId: goblin"));

            var loaded = AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(path);
            Assert.That(loaded, Is.Not.Null);
            AssertSameContent(loaded);
        }

        private void AssertSameContent(QuestGraphAsset loaded)
        {
            Assert.That(loaded.QuestId, Is.EqualTo("forest_cleanup"));
            Assert.That(loaded.DisplayName, Is.EqualTo("Зачистка леса"));
            Assert.That(loaded.Description, Is.EqualTo("Убить гоблинов"));
            Assert.That(loaded.RewardMode, Is.EqualTo(QuestRewardMode.ManualClaim));
            Assert.That(loaded.FormatVersion, Is.EqualTo(QuestGraphAsset.CurrentFormatVersion));

            Assert.That(loaded.Nodes.Count, Is.EqualTo(4));
            Assert.That(loaded.Nodes.Select(n => n.GetType()), Is.EqualTo(new[]
            {
                typeof(StartNodeData), typeof(KillObjective), typeof(EndNodeData), typeof(GoldReward)
            }));

            var loadedStart = loaded.FindNode(start.Id);
            Assert.That(loadedStart, Is.InstanceOf<StartNodeData>());
            Assert.That(loadedStart.Position, Is.EqualTo(new Vector2(10, 20)));

            var loadedKill = (KillObjective)loaded.FindNode(kill.Id);
            Assert.That(loadedKill.EnemyId, Is.EqualTo("goblin"));
            Assert.That(loadedKill.RequiredCount, Is.EqualTo(10));
            Assert.That(loadedKill.Title, Is.EqualTo("Гоблины"));
            Assert.That(loadedKill.ObjectiveId, Is.EqualTo(kill.ObjectiveId));

            var loadedEnd = (EndNodeData)loaded.FindNode(end.Id);
            Assert.That(loadedEnd.Result, Is.EqualTo(QuestEndResult.Completed));

            var loadedGold = (GoldReward)loaded.FindNode(gold.Id);
            Assert.That(loadedGold.Amount, Is.EqualTo(100));
            Assert.That(loadedGold.RewardId, Is.EqualTo(gold.RewardId));

            Assert.That(loaded.Edges.Count, Is.EqualTo(3));
            Assert.That(loaded.HasEdge(start.Id, QuestPortIds.Out, kill.Id, QuestPortIds.In), Is.True);
            Assert.That(loaded.HasEdge(kill.Id, QuestPortIds.Out, end.Id, QuestPortIds.In), Is.True);
            Assert.That(loaded.HasEdge(gold.Id, QuestPortIds.Reward, end.Id, QuestPortIds.Rewards), Is.True);
        }
    }
}
