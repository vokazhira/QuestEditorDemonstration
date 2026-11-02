using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Операции с ассетами заданий: поиск, создание, дублирование, удаление и подбор уникального questId.
    /// </summary>
    internal static class QuestAssetUtility
    {
        public const string DefaultQuestId = "quest";

        public static List<QuestGraphAsset> FindAllQuestAssets()
        {
            var result = new List<QuestGraphAsset>();
            var guids = AssetDatabase.FindAssets("t:" + nameof(QuestGraphAsset));
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            result.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return result;
        }

        /// <summary>
        /// Собирает questId всех заданий проекта, кроме ассета по пути <paramref name="excludePath"/>.
        /// </summary>
        public static HashSet<string> CollectQuestIds(string excludePath)
        {
            var ids = new HashSet<string>();
            foreach (var asset in FindAllQuestAssets())
            {
                if (!string.IsNullOrEmpty(excludePath) && AssetDatabase.GetAssetPath(asset) == excludePath)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(asset.QuestId))
                {
                    ids.Add(asset.QuestId);
                }
            }

            return ids;
        }

        public static string SuggestQuestId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return DefaultQuestId;
            }

            var builder = new StringBuilder(name.Length);
            foreach (var c in name.Trim().ToLowerInvariant())
            {
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            var result = builder.ToString().Trim('_');
            while (result.Contains("__"))
            {
                result = result.Replace("__", "_");
            }

            return result.Length == 0 ? DefaultQuestId : result;
        }

        public static string MakeUniqueQuestId(string desired, ICollection<string> existingIds)
        {
            var baseId = string.IsNullOrWhiteSpace(desired) ? DefaultQuestId : desired.Trim();
            if (existingIds == null || !existingIds.Contains(baseId))
            {
                return baseId;
            }

            for (var suffix = 2; ; suffix++)
            {
                var candidate = baseId + "_" + suffix;
                if (!existingIds.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// Заполняет пустой граф стартовым содержимым: узлы Start и End(Completed) без связи.
        /// </summary>
        public static void PopulateDefaultGraph(QuestGraphAsset graph)
        {
            if (graph == null || graph.Nodes.Count > 0)
            {
                return;
            }

            graph.AddNode(new StartNodeData { Position = new Vector2(0f, 0f) });
            graph.AddNode(new EndNodeData { Result = QuestEndResult.Completed, Position = new Vector2(400f, 0f) });
        }

        public static QuestGraphAsset CreateQuestAsset(string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var asset = ScriptableObject.CreateInstance<QuestGraphAsset>();
            asset.name = name;
            asset.QuestId = MakeUniqueQuestId(SuggestQuestId(name), CollectQuestIds(null));
            asset.DisplayName = name;
            PopulateDefaultGraph(asset);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        public static QuestGraphAsset DuplicateQuestAsset(QuestGraphAsset source)
        {
            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                return null;
            }

            var targetPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                return null;
            }

            var copy = AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(targetPath);
            if (copy == null)
            {
                return null;
            }

            var serializedCopy = new SerializedObject(copy);
            serializedCopy.FindProperty("questId").stringValue = MakeUniqueQuestId(source.QuestId, CollectQuestIds(targetPath));
            serializedCopy.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(copy);
            return copy;
        }

        /// <summary>
        /// Перемещает ассет в корзину операционной системы, чтобы удаление можно было отменить.
        /// </summary>
        public static bool DeleteQuestAsset(QuestGraphAsset asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(path) && AssetDatabase.MoveAssetToTrash(path);
        }
    }
}
