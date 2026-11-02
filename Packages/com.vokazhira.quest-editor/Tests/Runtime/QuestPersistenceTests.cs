using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vokazhira.QuestEditor.Tests
{
    public sealed class QuestPersistenceTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly List<QuestManager> managers = new List<QuestManager>();
        private TestLogHandler log;
        private InMemoryQuestSaveService storage;

        [SetUp]
        public void SetUp()
        {
            log = new TestLogHandler();
            storage = new InMemoryQuestSaveService();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var manager in managers)
            {
                manager.Dispose();
            }

            foreach (var created in createdObjects)
            {
                Object.DestroyImmediate(created);
            }

            managers.Clear();
            createdObjects.Clear();
        }

        [Test]
        public void SaveAndLoad_ContinuesFromSavedNodeAndProgress()
        {
            var kill = QuestTestGraphs.Kill("goblin", 3);
            var collect = QuestTestGraphs.Collect("herb", 2);
            var database = Database(Linear("chain", kill, collect));

            var first = CreateManager(database);
            first.Start("chain");
            first.Report(QuestEvent.EnemyKilled("goblin", 3));
            first.Report(QuestEvent.ItemCollected("herb"));
            Assert.That(first.Save(), Is.True, log.ToString());

            var second = CreateManager(database);
            Assert.That(second.Load(), Is.True, log.ToString());

            var instance = second.GetInstance("chain");
            Assert.That(instance.State, Is.EqualTo(QuestState.Active));
            Assert.That(instance.CurrentObjective, Is.SameAs(collect));
            Assert.That(instance.GetProgress(kill.ObjectiveId), Is.EqualTo(3));
            Assert.That(instance.GetProgress(collect.ObjectiveId), Is.EqualTo(1));

            second.Report(QuestEvent.ItemCollected("herb"));
            Assert.That(instance.State, Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void Capture_SkipsAvailableQuestsAndStoresStateByName()
        {
            var database = Database(Linear("a", QuestTestGraphs.Kill("goblin", 2)), Linear("b", QuestTestGraphs.Kill("orc", 1)));
            var manager = CreateManager(database);
            manager.Start("a");

            var data = manager.Capture();

            Assert.That(data.saveVersion, Is.EqualTo(QuestSaveData.CurrentVersion));
            Assert.That(data.quests.Count, Is.EqualTo(1));
            Assert.That(data.quests[0].questId, Is.EqualTo("a"));
            Assert.That(data.quests[0].state, Is.EqualTo("Active"));
            Assert.That(JsonUtility.ToJson(data), Does.Contain("\"state\":\"Active\""));
        }

        [Test]
        public void GrantedRewards_AreNotGrantedAgainAfterLoad()
        {
            var gold = new GoldReward { Amount = 100 };
            var xp = new ExperienceReward { Amount = 25 };
            var database = Database(Linear("partial", QuestRewardMode.AutoGrant, new RewardNodeData[] { gold, xp }, QuestTestGraphs.Kill("goblin", 1)));

            var firstCurrency = new FakeCurrencyService();
            var first = CreateManager(database);
            first.RegisterRewardHandler(new GoldRewardHandler(firstCurrency));
            first.Start("partial");
            first.Report(QuestEvent.EnemyKilled("goblin"));
            Assert.That(first.GetState("partial"), Is.EqualTo(QuestState.Claimable));
            Assert.That(firstCurrency.Gold, Is.EqualTo(100));
            first.Save();

            var secondCurrency = new FakeCurrencyService();
            var secondExperience = new FakeExperienceService();
            var second = CreateManager(database);
            second.RegisterRewardHandler(new GoldRewardHandler(secondCurrency));
            second.RegisterRewardHandler(new ExperienceRewardHandler(secondExperience));
            second.Load();

            Assert.That(second.GetState("partial"), Is.EqualTo(QuestState.Claimable));
            Assert.That(second.Claim("partial"), Is.True);
            Assert.That(secondCurrency.Gold, Is.EqualTo(0), "Gold was already granted before saving.");
            Assert.That(secondExperience.Experience, Is.EqualTo(25));
            Assert.That(second.GetState("partial"), Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void CompletedQuest_StaysCompletedAndIgnoresEventsAfterLoad()
        {
            var gold = new GoldReward { Amount = 10 };
            var database = Database(Linear("done", QuestRewardMode.AutoGrant, new RewardNodeData[] { gold }, QuestTestGraphs.Kill("goblin", 1)));
            var first = CreateManager(database);
            first.RegisterRewardHandler(new GoldRewardHandler(new FakeCurrencyService()));
            first.Start("done");
            first.Report(QuestEvent.EnemyKilled("goblin"));
            first.Save();

            var currency = new FakeCurrencyService();
            var second = CreateManager(database);
            second.RegisterRewardHandler(new GoldRewardHandler(currency));
            second.Load();
            second.Report(QuestEvent.EnemyKilled("goblin"));

            Assert.That(second.GetState("done"), Is.EqualTo(QuestState.Completed));
            Assert.That(second.CanStart("done"), Is.False);
            Assert.That(second.Claim("done"), Is.False);
            Assert.That(currency.Gold, Is.EqualTo(0));
        }

        [Test]
        public void UnknownQuest_IsKeptAsOrphanAndSavedBack()
        {
            var manager = CreateManager(Database(Linear("hunt", QuestTestGraphs.Kill("goblin", 1))));
            var data = new QuestSaveData();
            data.quests.Add(Record("ghost", "Active", "node-1", ("obj-1", 4)));

            Assert.That(manager.Restore(data), Is.True);

            var captured = manager.Capture();
            Assert.That(captured.quests, Is.Empty);
            Assert.That(captured.orphanedQuests.Count, Is.EqualTo(1));
            var orphan = captured.orphanedQuests[0];
            Assert.That(orphan.questId, Is.EqualTo("ghost"));
            Assert.That(orphan.state, Is.EqualTo("Active"));
            Assert.That(orphan.currentNodeId, Is.EqualTo("node-1"));
            Assert.That(orphan.progress[0].value, Is.EqualTo(4));
            Assert.That(log.Contains(LogType.Warning, "'ghost' is not in the database"), Is.True, log.ToString());
        }

        [Test]
        public void OrphanedRecord_IsRestoredWhenQuestReturnsToDatabase()
        {
            var kill = QuestTestGraphs.Kill("goblin", 5);
            var manager = CreateManager(Database(Linear("hunt", kill)));
            var data = new QuestSaveData();
            data.orphanedQuests.Add(Record("hunt", "Active", kill.Id, (kill.ObjectiveId, 2)));

            manager.Restore(data);

            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Active));
            Assert.That(manager.GetInstance("hunt").GetProgress(kill.ObjectiveId), Is.EqualTo(2));
            Assert.That(manager.Capture().orphanedQuests, Is.Empty);
        }

        [Test]
        public void MissingCurrentNode_FreezesQuestAndKeepsRecord()
        {
            var kill = QuestTestGraphs.Kill("goblin", 5);
            var manager = CreateManager(Database(Linear("hunt", kill)));
            var data = new QuestSaveData();
            data.quests.Add(Record("hunt", "Active", "deleted-node", (kill.ObjectiveId, 2)));

            manager.Restore(data);
            manager.Report(QuestEvent.EnemyKilled("goblin"));

            var instance = manager.GetInstance("hunt");
            Assert.That(instance.IsFrozen, Is.True);
            Assert.That(instance.State, Is.EqualTo(QuestState.Active));
            Assert.That(instance.CurrentObjective, Is.Null);
            Assert.That(instance.GetProgress(kill.ObjectiveId), Is.EqualTo(2), "A frozen quest receives no progress.");
            Assert.That(manager.CanStart("hunt"), Is.False);
            Assert.That(manager.Abandon("hunt"), Is.False);
            Assert.That(log.Contains(LogType.Warning, "'hunt' is frozen"), Is.True, log.ToString());

            var saved = manager.Capture().quests[0];
            Assert.That(saved.currentNodeId, Is.EqualTo("deleted-node"));
            Assert.That(saved.progress[0].value, Is.EqualTo(2));
        }

        [Test]
        public void UnknownStateName_FreezesQuest()
        {
            var manager = CreateManager(Database(Linear("hunt", QuestTestGraphs.Kill("goblin", 1))));
            var data = new QuestSaveData();
            data.quests.Add(Record("hunt", "Teleported", null));

            manager.Restore(data);

            Assert.That(manager.GetInstance("hunt").IsFrozen, Is.True);
            Assert.That(manager.Capture().quests[0].state, Is.EqualTo("Teleported"));
        }

        [Test]
        public void NewerSaveVersion_IsRefusedAndStateIsKept()
        {
            var manager = CreateManager(Database(Linear("hunt", QuestTestGraphs.Kill("goblin", 3))));
            manager.Start("hunt");
            var data = new QuestSaveData { saveVersion = QuestSaveData.CurrentVersion + 1 };

            Assert.That(manager.Restore(data), Is.False);

            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Active));
            Assert.That(log.Contains(LogType.Error, "newer than the supported version"), Is.True, log.ToString());
        }

        [Test]
        public void OlderSaveVersion_IsLoadedWithWarning()
        {
            var kill = QuestTestGraphs.Kill("goblin", 3);
            var manager = CreateManager(Database(Linear("hunt", kill)));
            var data = new QuestSaveData { saveVersion = 0 };
            data.quests.Add(Record("hunt", "Active", kill.Id, (kill.ObjectiveId, 1)));

            Assert.That(manager.Restore(data), Is.True);

            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Active));
            Assert.That(log.Contains(LogType.Warning, "older than the current version"), Is.True, log.ToString());
        }

        [Test]
        public void Restore_ResetsQuestsMissingFromSave()
        {
            var manager = CreateManager(Database(Linear("hunt", QuestTestGraphs.Kill("goblin", 3))));
            manager.Start("hunt");

            Assert.That(manager.Restore(new QuestSaveData()), Is.True);
            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Available));
            Assert.That(manager.CanStart("hunt"), Is.True);

            manager.Start("hunt");
            Assert.That(manager.Restore(null), Is.True);
            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Available));
        }

        [Test]
        public void SatisfiedObjectiveAfterLoad_AdvancesImmediately()
        {
            var kill = QuestTestGraphs.Kill("goblin", 3);
            var manager = CreateManager(Database(Linear("hunt", kill)));
            var data = new QuestSaveData();
            data.quests.Add(Record("hunt", "Active", kill.Id, (kill.ObjectiveId, 5)));

            manager.Restore(data);

            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void DuplicateRecords_FirstOneWins()
        {
            var kill = QuestTestGraphs.Kill("goblin", 5);
            var manager = CreateManager(Database(Linear("hunt", kill)));
            var data = new QuestSaveData();
            data.quests.Add(Record("hunt", "Active", kill.Id, (kill.ObjectiveId, 1)));
            data.quests.Add(Record("hunt", "Active", kill.Id, (kill.ObjectiveId, 4)));

            manager.Restore(data);

            Assert.That(manager.GetInstance("hunt").GetProgress(kill.ObjectiveId), Is.EqualTo(1));
            Assert.That(log.Contains(LogType.Warning, "several records"), Is.True, log.ToString());
        }

        [Test]
        public void Load_ServiceError_KeepsStateAndLogs()
        {
            var manager = CreateManager(Database(Linear("hunt", QuestTestGraphs.Kill("goblin", 3))));
            manager.Start("hunt");
            storage.LoadError = "broken file";

            Assert.That(manager.Load(), Is.False);

            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Active));
            Assert.That(log.Contains(LogType.Error, "broken file"), Is.True, log.ToString());
        }

        [Test]
        public void Save_ServiceThrows_ReturnsFalseAndLogs()
        {
            var manager = CreateManager(Database(Linear("hunt", QuestTestGraphs.Kill("goblin", 3))));
            storage.ThrowOnSave = true;

            Assert.That(manager.Save(), Is.False);
            Assert.That(log.Contains(LogType.Exception, "disk full"), Is.True, log.ToString());
        }

        [Test]
        public void SaveAndLoad_WithoutService_ReturnFalse()
        {
            var database = Database(Linear("hunt", QuestTestGraphs.Kill("goblin", 3)));
            var manager = new QuestManager(database, null, new Logger(log));
            managers.Add(manager);

            Assert.That(manager.Save(), Is.False);
            Assert.That(manager.Load(), Is.False);
            Assert.That(log.Count(LogType.Error), Is.EqualTo(2));
        }

        [Test]
        public void ActiveQuestWithInvalidGraph_IsFrozenOnLoad()
        {
            var kill = QuestTestGraphs.Kill("goblin", 3);
            var graph = Linear("hunt", kill);
            kill.EnemyId = string.Empty;
            var manager = CreateManager(Database(graph));
            var data = new QuestSaveData();
            data.quests.Add(Record("hunt", "Active", kill.Id, (kill.ObjectiveId, 1)));

            manager.Restore(data);

            Assert.That(manager.GetInstance("hunt").IsFrozen, Is.True);
        }

        private QuestManager CreateManager(QuestDatabase database)
        {
            var manager = new QuestManager(database, storage, new Logger(log));
            managers.Add(manager);
            return manager;
        }

        private QuestDatabase Database(params QuestGraphAsset[] quests)
        {
            var database = QuestTestGraphs.CreateDatabase(quests);
            createdObjects.Add(database);
            return database;
        }

        private QuestGraphAsset Linear(string questId, params ObjectiveNodeData[] objectives)
        {
            return Linear(questId, QuestRewardMode.AutoGrant, null, objectives);
        }

        private QuestGraphAsset Linear(string questId, QuestRewardMode mode, RewardNodeData[] rewards, params ObjectiveNodeData[] objectives)
        {
            var graph = QuestTestGraphs.CreateLinearQuest(questId, objectives, rewards, mode);
            createdObjects.Add(graph);
            return graph;
        }

        private static QuestInstanceSaveData Record(string questId, string state, string currentNodeId, params (string objectiveId, int value)[] progress)
        {
            var record = new QuestInstanceSaveData { questId = questId, state = state, currentNodeId = currentNodeId };
            foreach (var entry in progress)
            {
                record.progress.Add(new ObjectiveProgressEntry(entry.objectiveId, entry.value));
            }

            return record;
        }
    }
}
