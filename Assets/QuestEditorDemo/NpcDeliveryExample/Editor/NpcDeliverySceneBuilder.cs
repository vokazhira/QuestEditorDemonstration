using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery.Editor
{
    /// <summary>
    /// Создаёт пример «доставить письмо» рядом со своей папкой: задание, базу заданий и 3D-сцену с игроком и двумя NPC.
    /// Повторный запуск обновляет существующие ассеты и сохраняет их GUID.
    /// </summary>
    internal static class NpcDeliverySceneBuilder
    {
        private const string MenuPath = "Tools/Quest Editor/Samples/Build NPC Delivery Scene";
        private const string QuestId = "deliver_letter";

        [MenuItem(MenuPath)]
        private static void Build()
        {
            var root = FindRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog("NPC Delivery Example", "Could not find the sample folder.", "OK");
                return;
            }

            var questPath = root + "/DeliverTheLetter.asset";
            var databasePath = root + "/NpcDeliveryDatabase.asset";
            var scenePath = root + "/NpcDelivery.unity";

            if ((File.Exists(questPath) || File.Exists(databasePath) || File.Exists(scenePath))
                && !EditorUtility.DisplayDialog(
                    "NPC Delivery Example",
                    $"Sample assets already exist in '{root}'. Rebuild them? Changes made to them will be lost.",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var quest = CreateOrUpdateAsset(CreateQuest(), questPath);
            var database = CreateOrUpdateAsset(ScriptableObject.CreateInstance<QuestDatabase>(), databasePath);
            var serializedDatabase = new SerializedObject(database);
            var quests = serializedDatabase.FindProperty("quests");
            quests.arraySize = 1;
            quests.GetArrayElementAtIndex(0).objectReferenceValue = quest;
            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            if (!CreateScene(scenePath, databasePath))
            {
                return;
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("NPC Delivery Example", $"The sample was built in '{root}'. Open the NpcDelivery scene and press Play.", "OK");
        }

        /// <summary>
        /// «Deliver the Letter»: Start → доставить письмо кузнецу → End(Completed) с 50 золота, награда забирается у старосты.
        /// </summary>
        private static QuestGraphAsset CreateQuest()
        {
            var graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
            graph.QuestId = QuestId;
            graph.DisplayName = "Deliver the Letter";
            graph.Description = "The village elder asks you to take a letter to the blacksmith.";
            graph.RewardMode = QuestRewardMode.ManualClaim;

            var start = new StartNodeData { Position = new Vector2(0, 0) };
            var deliver = new DeliverItemObjective
            {
                ItemId = "letter",
                RecipientId = "blacksmith",
                Title = "Deliver the letter to the blacksmith",
                Description = "The blacksmith works at the forge across the village.",
                Position = new Vector2(240, 0)
            };
            var end = new EndNodeData { Result = QuestEndResult.Completed, Position = new Vector2(560, 0) };
            var gold = new GoldReward { Amount = 50, Position = new Vector2(300, 180) };

            graph.AddNode(start);
            graph.AddNode(deliver);
            graph.AddNode(end);
            graph.AddNode(gold);
            Connect(graph, start, QuestPortIds.Out, deliver, QuestPortIds.In);
            Connect(graph, deliver, QuestPortIds.Out, end, QuestPortIds.In);
            Connect(graph, gold, QuestPortIds.Reward, end, QuestPortIds.Rewards);
            return graph;
        }

        /// <summary>
        /// База загружается заново после <c>NewScene</c>: открытие сцены в режиме Single выгружает неиспользуемые ассеты.
        /// </summary>
        private static bool CreateScene(string scenePath, string databasePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var database = AssetDatabase.LoadAssetAtPath<QuestDatabase>(databasePath);
            if (database == null)
            {
                Debug.LogError($"NPC Delivery Example: quest database not found at '{databasePath}'.");
                return false;
            }

            var camera = Camera.main;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            var game = new GameObject("Game");
            var bootstrap = game.AddComponent<NpcDeliveryBootstrap>();
            var hud = game.AddComponent<NpcDeliveryHud>();
            Assign(bootstrap, "database", database);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();
            var body = player.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var controller = player.AddComponent<PlayerController>();
            var interactor = player.AddComponent<PlayerInteractor>();
            Assign(controller, "cameraTransform", camera != null ? camera.transform : null);
            Assign(interactor, "hud", hud);

            var elder = CreateNpc<QuestGiverNpc>("Elder", "elder", new Vector3(-6f, 1f, 5f), new Color(0.3f, 0.75f, 0.35f), bootstrap);
            Assign(elder, "questId", QuestId);
            Assign(elder, "itemToGive", "letter");

            var blacksmith = CreateNpc<DeliveryRecipientNpc>("Blacksmith", "blacksmith", new Vector3(9f, 1f, -7f), new Color(0.95f, 0.55f, 0.15f), bootstrap);
            Assign(blacksmith, "expectedItemId", "letter");

            var serializedHud = new SerializedObject(hud);
            serializedHud.FindProperty("bootstrap").objectReferenceValue = bootstrap;
            serializedHud.FindProperty("interactor").objectReferenceValue = interactor;
            serializedHud.FindProperty("worldCamera").objectReferenceValue = camera;
            var npcs = serializedHud.FindProperty("npcs");
            npcs.arraySize = 2;
            npcs.GetArrayElementAtIndex(0).objectReferenceValue = elder;
            npcs.GetArrayElementAtIndex(1).objectReferenceValue = blacksmith;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();

            return EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static T CreateNpc<T>(string displayName, string npcId, Vector3 position, Color color, NpcDeliveryBootstrap bootstrap)
            where T : Npc
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = displayName;
            body.transform.position = position;

            var talkZone = body.AddComponent<SphereCollider>();
            talkZone.isTrigger = true;
            talkZone.radius = 2.5f;

            var npc = body.AddComponent<T>();
            var serializedNpc = new SerializedObject(npc);
            serializedNpc.FindProperty("npcId").stringValue = npcId;
            serializedNpc.FindProperty("displayName").stringValue = displayName;
            serializedNpc.FindProperty("color").colorValue = color;
            serializedNpc.FindProperty("bootstrap").objectReferenceValue = bootstrap;
            serializedNpc.ApplyModifiedPropertiesWithoutUndo();
            return npc;
        }

        private static void Assign(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T CreateOrUpdateAsset<T>(T asset, string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }

                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            asset.name = existing.name;
            EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void Connect(QuestGraphAsset graph, QuestNodeData from, string fromPort, QuestNodeData to, string toPort)
        {
            if (!graph.TryConnect(from.Id, fromPort, to.Id, toPort, out var reason))
            {
                Debug.LogError($"NPC Delivery Example: could not connect nodes: {reason}");
            }
        }

        /// <summary>
        /// Папка примера — родитель папки Editor, в которой лежит этот скрипт.
        /// </summary>
        private static string FindRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets(nameof(NpcDeliverySceneBuilder) + " t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == nameof(NpcDeliverySceneBuilder))
                {
                    return Path.GetDirectoryName(Path.GetDirectoryName(path))?.Replace('\\', '/');
                }
            }

            return null;
        }
    }
}
