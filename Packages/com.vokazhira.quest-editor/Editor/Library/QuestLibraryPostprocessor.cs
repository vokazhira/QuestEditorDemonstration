using System;
using UnityEditor;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Сообщает открытым окнам, что ассеты .asset изменились. Уведомление откладывается через
    /// <see cref="EditorApplication.delayCall"/> и объединяет пачку изменений в один вызов.
    /// </summary>
    internal sealed class QuestLibraryPostprocessor : AssetPostprocessor
    {
        private static bool notificationScheduled;

        public static event Action QuestAssetsChanged;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (notificationScheduled || QuestAssetsChanged == null)
            {
                return;
            }

            if (!ContainsAsset(importedAssets)
                && !ContainsAsset(deletedAssets)
                && !ContainsAsset(movedAssets)
                && !ContainsAsset(movedFromAssetPaths))
            {
                return;
            }

            notificationScheduled = true;
            EditorApplication.delayCall += Notify;
        }

        private static void Notify()
        {
            notificationScheduled = false;
            QuestAssetsChanged?.Invoke();
        }

        private static bool ContainsAsset(string[] paths)
        {
            foreach (var path in paths)
            {
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
