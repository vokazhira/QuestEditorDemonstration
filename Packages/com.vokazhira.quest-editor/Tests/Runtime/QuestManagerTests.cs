using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vokazhira.QuestEditor.Tests
{
    public sealed class QuestManagerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private FakeCurrencyService currency;
        private FakeExperienceService experience;
        private TestLogHandler log;
        private QuestManager manager;

        [SetUp]
        public void SetUp()
        {
            currency = new FakeCurrencyService();
            experience = new FakeExperienceService();
            log = new TestLogHandler();
        }

        [TearDown]
        public void TearDown()
        {
            manager?.Dispose();
            manager = null;
            foreach (var created in createdObjects)
            {
                Object.DestroyImmediate(created);
            }

            createdObjects.Clear();
        }

        [Test]
        public void Start_ValidQuest_BecomesActiveOnFirstObjective()
        {
            var kill = QuestTestGraphs.Kill("goblin", 3);
            CreateManager(Linear("hunt", kill));
            var log = new QuestEventLog(manager);

            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Available));
            Assert.That(manager.CanStart("hunt"), Is.True);
            Assert.That(manager.Start("hunt"), Is.True);

            var instance = manager.GetInstance("hunt");
            Assert.That(instance.State, Is.EqualTo(QuestState.Active));
            Assert.That(instance.CurrentObjective, Is.SameAs(kill));
            Assert.That(log.Entries, Is.EqualTo(new[] { "started:hunt" }));
        }

        [Test]
        public void Report_MatchingEvent_IncreasesProgress()
        {
            var kill = QuestTestGraphs.Kill("goblin", 3);
            CreateManager(Linear("hunt", kill));
            var log = new QuestEventLog(manager);
            manager.Start("hunt");

            manager.Report(QuestEvent.EnemyKilled("goblin"));
            manager.Report(QuestEvent.EnemyKilled("goblin"));

            Assert.That(manager.GetInstance("hunt").GetProgress(kill.ObjectiveId), Is.EqualTo(2));
            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Active));
            Assert.That(log.Entries, Is.EqualTo(new[] { "started:hunt", "progress:hunt:1/3", "progress:hunt:2/3" }));
        }

        [Test]
        public void Report_NonMatchingEvent_DoesNothing()
        {
            var kill = QuestTestGraphs.Kill("goblin", 3);
            CreateManager(Linear("hunt", kill));
            var log = new QuestEventLog(manager);
            manager.Start("hunt");

            manager.Report(QuestEvent.EnemyKilled("orc"));
            manager.Report(QuestEvent.ItemCollected("goblin"));

            Assert.That(manager.GetInstance("hunt").GetProgress(kill.ObjectiveId), Is.EqualTo(0));
            Assert.That(log.Count("progress"), Is.EqualTo(0));
        }

        [Test]
        public void Report_BeforeStart_IsIgnored()
        {
            var kill = QuestTestGraphs.Kill("goblin", 1);
            CreateManager(Linear("hunt", kill));

            manager.Report(QuestEvent.EnemyKilled("goblin"));

            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Available));
            Assert.That(manager.GetInstance("hunt").GetProgress(kill.ObjectiveId), Is.EqualTo(0));
        }

        [Test]
        public void Progress_DoesNotExceedRequiredCount()
        {
            var kill = QuestTestGraphs.Kill("goblin", 3);
            CreateManager(Linear("hunt", kill));
            var log = new QuestEventLog(manager);
            manager.Start("hunt");

            manager.Report(QuestEvent.EnemyKilled("goblin", 10));

            Assert.That(manager.GetInstance("hunt").GetProgress(kill.ObjectiveId), Is.EqualTo(3));
            Assert.That(log.Entries, Does.Contain("progress:hunt:3/3"));
            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void SequentialObjectives_AreCompletedInOrder()
        {
            var kill = QuestTestGraphs.Kill("goblin", 2);
            var collect = QuestTestGraphs.Collect("herb", 1);
            CreateManager(Linear("chain", kill, collect));
            manager.Start("chain");
            var instance = manager.GetInstance("chain");

            manager.Report(QuestEvent.ItemCollected("herb"));
            Assert.That(instance.GetProgress(collect.ObjectiveId), Is.EqualTo(0), "Вторая цель ещё не активна.");

            manager.Report(QuestEvent.EnemyKilled("goblin", 2));
            Assert.That(instance.CurrentObjective, Is.SameAs(collect));

            manager.Report(QuestEvent.EnemyKilled("goblin"));
            Assert.That(instance.GetProgress(kill.ObjectiveId), Is.EqualTo(2), "Пройденная цель больше не получает прогресс.");

            manager.Report(QuestEvent.ItemCollected("herb"));
            Assert.That(instance.State, Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void ConditionTrue_LeadsToCompletedEndAndAutoGrantsRewards()
        {
            var intro = Linear("intro", QuestTestGraphs.Collect("letter", 1));
            var forest = Forest();
            CreateManager(intro, forest.Graph);
            RegisterBuiltInHandlers();
            var log = new QuestEventLog(manager);

            manager.Start("intro");
            manager.Report(QuestEvent.ItemCollected("letter"));
            Assert.That(manager.GetState("intro"), Is.EqualTo(QuestState.Completed));

            manager.Start("forest_cleanup");
            manager.Report(QuestEvent.EnemyKilled("goblin", 10));

            Assert.That(manager.GetState("forest_cleanup"), Is.EqualTo(QuestState.Completed));
            Assert.That(manager.GetInstance("forest_cleanup").CurrentNodeId, Is.EqualTo(forest.Completed.Id));
            Assert.That(currency.Gold, Is.EqualTo(100));
            Assert.That(experience.Experience, Is.EqualTo(25));
            Assert.That(log.Entries.GetRange(log.Entries.Count - 3, 3), Is.EqualTo(new[]
            {
                "granted:forest_cleanup:+100 золота",
                "granted:forest_cleanup:+25 опыта",
                "completed:forest_cleanup"
            }));
        }

        [Test]
        public void ConditionFalse_LeadsToFailedEndWithoutRewards()
        {
            var intro = Linear("intro", QuestTestGraphs.Collect("letter", 1));
            var forest = Forest();
            CreateManager(intro, forest.Graph);
            RegisterBuiltInHandlers();
            var log = new QuestEventLog(manager);

            manager.Start("forest_cleanup");
            manager.Report(QuestEvent.EnemyKilled("goblin", 10));

            Assert.That(manager.GetState("forest_cleanup"), Is.EqualTo(QuestState.Failed));
            Assert.That(manager.GetInstance("forest_cleanup").CurrentNodeId, Is.EqualTo(forest.Failed.Id));
            Assert.That(currency.Gold, Is.EqualTo(0));
            Assert.That(log.Entries, Does.Contain("failed:forest_cleanup"));
            Assert.That(log.Count("completed"), Is.EqualTo(0));
        }

        [Test]
        public void ManualClaim_WaitsForClaimThenGrantsRewards()
        {
            var gold = new GoldReward { Amount = 50 };
            CreateManager(Linear("manual", QuestRewardMode.ManualClaim, new RewardNodeData[] { gold }, QuestTestGraphs.Kill("goblin", 1)));
            RegisterBuiltInHandlers();
            var log = new QuestEventLog(manager);
            manager.Start("manual");

            manager.Report(QuestEvent.EnemyKilled("goblin"));

            Assert.That(manager.GetState("manual"), Is.EqualTo(QuestState.Claimable));
            Assert.That(currency.Gold, Is.EqualTo(0));
            Assert.That(log.Entries, Does.Contain("claimable:manual"));

            Assert.That(manager.Claim("manual"), Is.True);

            Assert.That(manager.GetState("manual"), Is.EqualTo(QuestState.Completed));
            Assert.That(currency.Gold, Is.EqualTo(50));
            Assert.That(manager.GetInstance("manual").IsRewardGranted(gold.RewardId), Is.True);
            Assert.That(manager.Claim("manual"), Is.False, "Повторный Claim ничего не делает.");
            Assert.That(currency.Gold, Is.EqualTo(50));
        }

        [Test]
        public void MissingRewardHandler_KeepsQuestClaimableUntilHandlerRegistered()
        {
            var gold = new GoldReward { Amount = 100 };
            var xp = new ExperienceReward { Amount = 25 };
            CreateManager(Linear("partial", QuestRewardMode.AutoGrant, new RewardNodeData[] { gold, xp }, QuestTestGraphs.Kill("goblin", 1)));
            manager.RegisterRewardHandler(new GoldRewardHandler(currency));
            var log = new QuestEventLog(manager);
            manager.Start("partial");

            manager.Report(QuestEvent.EnemyKilled("goblin"));

            Assert.That(manager.GetState("partial"), Is.EqualTo(QuestState.Claimable));
            Assert.That(currency.Gold, Is.EqualTo(100));
            Assert.That(log.Entries, Does.Contain("reward-failed:partial:+25 опыта"));
            Assert.That(log.Entries, Does.Contain("claimable:partial"));

            Assert.That(manager.Claim("partial"), Is.False, "Обработчика опыта всё ещё нет.");

            manager.RegisterRewardHandler(new ExperienceRewardHandler(experience));
            Assert.That(manager.Claim("partial"), Is.True);

            Assert.That(manager.GetState("partial"), Is.EqualTo(QuestState.Completed));
            Assert.That(currency.Gold, Is.EqualTo(100), "Уже выданное золото не выдаётся повторно.");
            Assert.That(experience.Experience, Is.EqualTo(25));
        }

        [Test]
        public void RewardHandlerReturningFalse_CanBeRetriedWithClaim()
        {
            var gold = new GoldReward { Amount = 30 };
            CreateManager(Linear("retry", QuestRewardMode.AutoGrant, new RewardNodeData[] { gold }, QuestTestGraphs.Kill("goblin", 1)));
            RegisterBuiltInHandlers();
            currency.Accept = false;
            manager.Start("retry");

            manager.Report(QuestEvent.EnemyKilled("goblin"));
            Assert.That(manager.GetState("retry"), Is.EqualTo(QuestState.Claimable));

            currency.Accept = true;
            Assert.That(manager.Claim("retry"), Is.True);
            Assert.That(currency.Gold, Is.EqualTo(30));
        }

        [Test]
        public void EventsAfterCompletion_AreIgnored()
        {
            var kill = QuestTestGraphs.Kill("goblin", 1);
            CreateManager(Linear("hunt", kill));
            manager.Start("hunt");
            manager.Report(QuestEvent.EnemyKilled("goblin"));
            var log = new QuestEventLog(manager);

            manager.Report(QuestEvent.EnemyKilled("goblin"));
            manager.Report(QuestEvent.EnemyKilled("goblin"));

            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Completed));
            Assert.That(manager.GetInstance("hunt").GetProgress(kill.ObjectiveId), Is.EqualTo(1));
            Assert.That(log.Entries, Is.Empty);
        }

        [Test]
        public void InvalidGraph_IsRegisteredButRefusedToStart()
        {
            var broken = Linear("broken", QuestTestGraphs.Kill(string.Empty, 1));
            CreateManager(broken);

            Assert.That(log.Contains(LogType.Error, "«broken» содержит ошибки"), Is.True, log.ToString());
            Assert.That(manager.GetValidationErrors("broken"), Is.Not.Empty);
            Assert.That(manager.CanStart("broken"), Is.False);
            Assert.That(manager.Start("broken"), Is.False);
            Assert.That(log.Contains(LogType.Warning, "«broken» нельзя запустить"), Is.True, log.ToString());
            Assert.That(manager.GetState("broken"), Is.EqualTo(QuestState.Available));
        }

        [Test]
        public void ValidQuest_HasNoValidationErrors()
        {
            CreateManager(Linear("hunt", QuestTestGraphs.Kill("goblin", 1)));

            Assert.That(manager.GetValidationErrors("hunt"), Is.Empty);
            Assert.That(log.Count(LogType.Error), Is.EqualTo(0), log.ToString());
        }

        [Test]
        public void WithoutLogger_UsesUnityLoggerAndDoesNotThrow()
        {
            var database = QuestTestGraphs.CreateDatabase(Linear("hunt", QuestTestGraphs.Kill("goblin", 1)));
            createdObjects.Add(database);

            manager = new QuestManager(database, null);

            Assert.That(manager.Start("hunt"), Is.True);
        }

        [Test]
        public void Start_AlreadyActiveOrUnknownQuest_ReturnsFalse()
        {
            CreateManager(Linear("hunt", QuestTestGraphs.Kill("goblin", 1)));
            manager.Start("hunt");

            Assert.That(manager.Start("hunt"), Is.False);
            Assert.That(manager.Start("missing"), Is.False);
            Assert.That(log.Contains(LogType.Warning, "«hunt» нельзя запустить"), Is.True, log.ToString());
            Assert.That(log.Contains(LogType.Warning, "«missing» нельзя запустить"), Is.True, log.ToString());
            Assert.That(manager.GetState("missing"), Is.EqualTo(QuestState.Available));
            Assert.That(manager.GetInstance("missing"), Is.Null);
        }

        [Test]
        public void Abandon_ActiveQuest_BecomesFailed()
        {
            CreateManager(Linear("hunt", QuestTestGraphs.Kill("goblin", 5)));
            var log = new QuestEventLog(manager);

            Assert.That(manager.Abandon("hunt"), Is.False, "Нельзя прервать не начатое задание.");
            manager.Start("hunt");

            Assert.That(manager.Abandon("hunt"), Is.True);
            Assert.That(manager.GetState("hunt"), Is.EqualTo(QuestState.Failed));
            Assert.That(log.Entries, Does.Contain("failed:hunt"));
            Assert.That(manager.Abandon("hunt"), Is.False);
        }

        [Test]
        public void Claim_NotClaimableQuest_ReturnsFalse()
        {
            CreateManager(Linear("hunt", QuestTestGraphs.Kill("goblin", 1)));

            Assert.That(manager.Claim("hunt"), Is.False);
            manager.Start("hunt");
            Assert.That(manager.Claim("hunt"), Is.False);
            Assert.That(manager.Claim("missing"), Is.False);
        }

        [Test]
        public void GetInstances_ListsAllRegisteredQuestsInDatabaseOrder()
        {
            CreateManager(Linear("b", QuestTestGraphs.Kill("goblin", 1)), Linear("a", QuestTestGraphs.Kill("orc", 1)));

            var ids = new List<string>();
            foreach (var instance in manager.GetInstances())
            {
                ids.Add(instance.QuestId);
            }

            Assert.That(ids, Is.EqualTo(new[] { "b", "a" }));
        }

        [Test]
        public void SameEventProgressesAllActiveQuests()
        {
            CreateManager(Linear("a", QuestTestGraphs.Kill("goblin", 2)), Linear("b", QuestTestGraphs.Kill("goblin", 1)));
            manager.Start("a");
            manager.Start("b");

            manager.Report(QuestEvent.EnemyKilled("goblin"));

            Assert.That(manager.GetState("a"), Is.EqualTo(QuestState.Active));
            Assert.That(manager.GetState("b"), Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void Dispose_ClearsSubscribersAndRejectsFurtherCalls()
        {
            CreateManager(Linear("hunt", QuestTestGraphs.Kill("goblin", 1)));
            var started = 0;
            manager.QuestStarted += _ => started++;

            manager.Dispose();

            Assert.Throws<ObjectDisposedException>(() => manager.Start("hunt"));
            Assert.Throws<ObjectDisposedException>(() => manager.Report(QuestEvent.EnemyKilled("goblin")));
            Assert.That(manager.CanStart("hunt"), Is.False);
            Assert.That(started, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_NullDatabase_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new QuestManager(null, null));
        }

        private void CreateManager(params QuestGraphAsset[] quests)
        {
            var database = QuestTestGraphs.CreateDatabase(quests);
            createdObjects.Add(database);
            manager = new QuestManager(database, null, new Logger(log));
        }

        private void RegisterBuiltInHandlers()
        {
            manager.RegisterRewardHandler(new GoldRewardHandler(currency));
            manager.RegisterRewardHandler(new ExperienceRewardHandler(experience));
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

        private QuestTestGraphs.ForestQuest Forest()
        {
            var forest = QuestTestGraphs.CreateForestQuest();
            createdObjects.Add(forest.Graph);
            return forest;
        }
    }
}
