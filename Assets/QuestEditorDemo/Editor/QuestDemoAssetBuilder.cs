using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vokazhira.QuestEditor.Demo.Editor
{
    /// <summary>
    /// Создаёт ассеты демо рядом с этой папкой: два задания, базу заданий и сцену с подключёнными компонентами.
    /// Задания собираются через публичный API пакета — так же их можно собрать в окне Quest Editor.
    /// </summary>
    internal static class QuestDemoAssetBuilder
    {
        private const string MenuPath = "Tools/Quest Editor/Demo/Build Demo Assets";

        [MenuItem(MenuPath)]
        private static void Build()
        {
            var root = FindDemoRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog("Quest Editor Demo", "Не удалось найти папку демо.", "OK");
                return;
            }

            var questsFolder = root + "/Quests";
            var introPath = questsFolder + "/IntroQuest.asset";
            var forestPath = questsFolder + "/ForestCleanup.asset";
            var databasePath = root + "/QuestDemoDatabase.asset";
            var scenePath = root + "/QuestDemo.unity";

            var exists = File.Exists(introPath) || File.Exists(forestPath) || File.Exists(databasePath) || File.Exists(scenePath);
            if (exists && !EditorUtility.DisplayDialog(
                    "Quest Editor Demo",
                    $"Ассеты демо в «{root}» уже существуют. Пересоздать их? Изменения в них будут потеряны.",
                    "Пересоздать",
                    "Отмена"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(questsFolder))
            {
                AssetDatabase.CreateFolder(root, "Quests");
            }

            var intro = CreateIntroQuest();
            var forest = CreateForestQuest();
            ReplaceAsset(intro, introPath);
            ReplaceAsset(forest, forestPath);

            var database = ScriptableObject.CreateInstance<QuestDatabase>();
            ReplaceAsset(database, databasePath);
            var serializedDatabase = new SerializedObject(database);
            var quests = serializedDatabase.FindProperty("quests");
            quests.arraySize = 2;
            quests.GetArrayElementAtIndex(0).objectReferenceValue = intro;
            quests.GetArrayElementAtIndex(1).objectReferenceValue = forest;
            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            CreateScene(scenePath, database);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Quest Editor Demo", $"Демо создано в «{root}». Откройте сцену QuestDemo и нажмите Play.", "OK");
        }

        /// <summary>
        /// «Письмо старосты»: найти письмо → выполнено, 10 золота.
        /// </summary>
        private static QuestGraphAsset CreateIntroQuest()
        {
            var graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
            graph.QuestId = "intro";
            graph.DisplayName = "Письмо старосты";
            graph.Description = "Найдите письмо старосты деревни.";
            graph.RewardMode = QuestRewardMode.AutoGrant;

            var start = new StartNodeData { Position = new Vector2(0, 0) };
            var letter = new CollectObjective
            {
                ItemId = "letter",
                Title = "Найти письмо",
                Description = "Письмо где-то в деревне.",
                Position = new Vector2(220, 0)
            };
            letter.SetRequiredCount(1);
            var end = new EndNodeData { Result = QuestEndResult.Completed, Position = new Vector2(520, 0) };
            var gold = new GoldReward { Amount = 10, Position = new Vector2(220, 180) };

            graph.AddNode(start);
            graph.AddNode(letter);
            graph.AddNode(end);
            graph.AddNode(gold);
            Connect(graph, start, QuestPortIds.Out, letter, QuestPortIds.In);
            Connect(graph, letter, QuestPortIds.Out, end, QuestPortIds.In);
            Connect(graph, gold, QuestPortIds.Reward, end, QuestPortIds.Rewards);
            return graph;
        }

        /// <summary>
        /// «Зачистка леса» из спецификации: убить 10 гоблинов, затем проверка «письмо старосты выполнено?»;
        /// да — выполнено (100 золота, 25 опыта, награда по кнопке), нет — провал.
        /// </summary>
        private static QuestGraphAsset CreateForestQuest()
        {
            var graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
            graph.QuestId = "forest_cleanup";
            graph.DisplayName = "Зачистка леса";
            graph.Description = "Перебейте гоблинов в лесу. Староста заплатит, только если вы уже прочли его письмо.";
            graph.RewardMode = QuestRewardMode.ManualClaim;

            var start = new StartNodeData { Position = new Vector2(0, 100) };
            var kill = new KillObjective
            {
                EnemyId = "goblin",
                Title = "Убить гоблинов",
                Description = "Гоблины засели в лесу.",
                Position = new Vector2(220, 100)
            };
            kill.SetRequiredCount(10);
            var condition = new QuestCompletedCondition { QuestId = "intro", Position = new Vector2(520, 100) };
            var completed = new EndNodeData { Result = QuestEndResult.Completed, Position = new Vector2(820, 0) };
            var failed = new EndNodeData { Result = QuestEndResult.Failed, Position = new Vector2(820, 220) };
            var gold = new GoldReward { Amount = 100, Position = new Vector2(520, -160) };
            var experience = new ExperienceReward { Amount = 25, Position = new Vector2(520, -60) };

            graph.AddNode(start);
            graph.AddNode(kill);
            graph.AddNode(condition);
            graph.AddNode(completed);
            graph.AddNode(failed);
            graph.AddNode(gold);
            graph.AddNode(experience);
            Connect(graph, start, QuestPortIds.Out, kill, QuestPortIds.In);
            Connect(graph, kill, QuestPortIds.Out, condition, QuestPortIds.In);
            Connect(graph, condition, QuestPortIds.True, completed, QuestPortIds.In);
            Connect(graph, condition, QuestPortIds.False, failed, QuestPortIds.In);
            Connect(graph, gold, QuestPortIds.Reward, completed, QuestPortIds.Rewards);
            Connect(graph, experience, QuestPortIds.Reward, completed, QuestPortIds.Rewards);
            return graph;
        }

        private static void CreateScene(string scenePath, QuestDatabase database)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var demo = new GameObject("QuestDemo");
            var bootstrap = demo.AddComponent<QuestDemoBootstrap>();
            var ui = demo.AddComponent<QuestDemoUI>();

            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("database").objectReferenceValue = database;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            var serializedUi = new SerializedObject(ui);
            serializedUi.FindProperty("bootstrap").objectReferenceValue = bootstrap;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void ReplaceAsset(Object asset, string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(asset, path);
        }

        private static void Connect(QuestGraphAsset graph, QuestNodeData from, string fromPort, QuestNodeData to, string toPort)
        {
            if (!graph.TryConnect(from.Id, fromPort, to.Id, toPort, out var reason))
            {
                Debug.LogError($"Quest Editor Demo: не удалось соединить узлы: {reason}");
            }
        }

        /// <summary>
        /// Папка демо — родитель папки Editor, в которой лежит этот скрипт. Работает и в Assets/QuestEditorDemo,
        /// и после импорта примера из Package Manager.
        /// </summary>
        private static string FindDemoRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets(nameof(QuestDemoAssetBuilder) + " t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == nameof(QuestDemoAssetBuilder))
                {
                    var editorFolder = Path.GetDirectoryName(path);
                    return Path.GetDirectoryName(editorFolder)?.Replace('\\', '/');
                }
            }

            return null;
        }
    }
}
