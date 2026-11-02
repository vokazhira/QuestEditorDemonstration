using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vokazhira.QuestEditor.Tests
{
    /// <summary>
    /// Построение графов для тестов, включая заведомо повреждённые данные, которые нельзя получить через публичный API.
    /// </summary>
    internal static class QuestTestGraphs
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// Пример «Зачистка леса» из спецификации: валидный граф без ошибок и предупреждений.
        /// </summary>
        public sealed class ForestQuest
        {
            public QuestGraphAsset Graph;
            public StartNodeData Start;
            public KillObjective Kill;
            public QuestCompletedCondition Condition;
            public EndNodeData Completed;
            public EndNodeData Failed;
            public GoldReward Gold;
            public ExperienceReward Experience;
        }

        public static ForestQuest CreateForestQuest(string questId = "forest_cleanup")
        {
            var quest = new ForestQuest
            {
                Graph = ScriptableObject.CreateInstance<QuestGraphAsset>(),
                Start = new StartNodeData(),
                Kill = new KillObjective { EnemyId = "goblin", Title = "Гоблины", Description = "Убейте гоблинов в лесу" },
                Condition = new QuestCompletedCondition { QuestId = "intro" },
                Completed = new EndNodeData { Result = QuestEndResult.Completed },
                Failed = new EndNodeData { Result = QuestEndResult.Failed },
                Gold = new GoldReward { Amount = 100 },
                Experience = new ExperienceReward { Amount = 25 }
            };
            quest.Kill.SetRequiredCount(10);

            var graph = quest.Graph;
            graph.name = questId;
            graph.QuestId = questId;
            graph.DisplayName = "Зачистка леса";
            graph.AddNode(quest.Start);
            graph.AddNode(quest.Kill);
            graph.AddNode(quest.Condition);
            graph.AddNode(quest.Completed);
            graph.AddNode(quest.Failed);
            graph.AddNode(quest.Gold);
            graph.AddNode(quest.Experience);

            Connect(graph, quest.Start, QuestPortIds.Out, quest.Kill, QuestPortIds.In);
            Connect(graph, quest.Kill, QuestPortIds.Out, quest.Condition, QuestPortIds.In);
            Connect(graph, quest.Condition, QuestPortIds.True, quest.Completed, QuestPortIds.In);
            Connect(graph, quest.Condition, QuestPortIds.False, quest.Failed, QuestPortIds.In);
            Connect(graph, quest.Gold, QuestPortIds.Reward, quest.Completed, QuestPortIds.Rewards);
            Connect(graph, quest.Experience, QuestPortIds.Reward, quest.Completed, QuestPortIds.Rewards);
            return quest;
        }

        /// <summary>
        /// Линейное задание Start → цели по порядку → End(Completed) с наградами.
        /// </summary>
        public static QuestGraphAsset CreateLinearQuest(
            string questId,
            ObjectiveNodeData[] objectives,
            RewardNodeData[] rewards = null,
            QuestRewardMode rewardMode = QuestRewardMode.AutoGrant)
        {
            var graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
            graph.name = questId;
            graph.QuestId = questId;
            graph.RewardMode = rewardMode;

            var start = new StartNodeData();
            var end = new EndNodeData { Result = QuestEndResult.Completed };
            graph.AddNode(start);
            graph.AddNode(end);

            QuestNodeData previous = start;
            var previousPort = QuestPortIds.Out;
            foreach (var objective in objectives)
            {
                graph.AddNode(objective);
                Connect(graph, previous, previousPort, objective, QuestPortIds.In);
                previous = objective;
                previousPort = QuestPortIds.Out;
            }

            Connect(graph, previous, previousPort, end, QuestPortIds.In);

            foreach (var reward in rewards ?? Array.Empty<RewardNodeData>())
            {
                graph.AddNode(reward);
                Connect(graph, reward, QuestPortIds.Reward, end, QuestPortIds.Rewards);
            }

            return graph;
        }

        public static KillObjective Kill(string enemyId, int count)
        {
            var objective = new KillObjective { EnemyId = enemyId, Description = "Убить " + enemyId };
            objective.SetRequiredCount(count);
            return objective;
        }

        public static CollectObjective Collect(string itemId, int count)
        {
            var objective = new CollectObjective { ItemId = itemId, Description = "Собрать " + itemId };
            objective.SetRequiredCount(count);
            return objective;
        }

        public static QuestDatabase CreateDatabase(params QuestGraphAsset[] quests)
        {
            var database = ScriptableObject.CreateInstance<QuestDatabase>();
            database.SetQuests(quests);
            return database;
        }

        public static void Connect(QuestGraphAsset graph, QuestNodeData from, string fromPort, QuestNodeData to, string toPort)
        {
            if (!graph.TryConnect(from.Id, fromPort, to.Id, toPort, out var reason))
            {
                throw new InvalidOperationException(reason);
            }
        }

        public static void AddRawEdge(QuestGraphAsset graph, string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            GetList<QuestEdgeData>(graph, "edges").Add(new QuestEdgeData(fromNodeId, fromPortId, toNodeId, toPortId));
        }

        public static void AddRawNode(QuestGraphAsset graph, QuestNodeData node)
        {
            GetList<QuestNodeData>(graph, "nodes").Add(node);
            graph.InvalidateNodeIndex();
        }

        public static void SetNodeId(QuestNodeData node, string id)
        {
            typeof(QuestNodeData).GetField("id", PrivateInstance).SetValue(node, id);
        }

        private static List<T> GetList<T>(QuestGraphAsset graph, string fieldName)
        {
            return (List<T>)typeof(QuestGraphAsset).GetField(fieldName, PrivateInstance).GetValue(graph);
        }
    }
}
