using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Запуск <see cref="QuestGraphValidator"/> из редактора: подставляет остальные задания проекта для проверки
    /// уникальности questId и добавляет имена отсутствующих классов узлов (доступно только в редакторе).
    /// </summary>
    internal static class QuestEditorValidation
    {
        /// <param name="graph">Проверяемый граф (рабочая копия или ассет).</param>
        /// <param name="sourceAsset">Ассет, из которого открыта рабочая копия; исключается из проверки questId.</param>
        /// <param name="allQuests">Все задания проекта; если null — будут найдены через AssetDatabase.</param>
        public static List<QuestDiagnostic> Validate(QuestGraphAsset graph, QuestGraphAsset sourceAsset, IReadOnlyList<QuestGraphAsset> allQuests = null)
        {
            var quests = allQuests ?? QuestAssetUtility.FindAllQuestAssets();
            var others = new List<QuestGraphAsset>();
            foreach (var quest in quests)
            {
                if (quest != null && quest != graph && quest != sourceAsset)
                {
                    others.Add(quest);
                }
            }

            var diagnostics = QuestGraphValidator.Validate(graph, others);
            InsertMissingTypeDetails(sourceAsset != null ? sourceAsset : graph, diagnostics);
            return diagnostics;
        }

        public static bool HasMissingTypes(Object target)
        {
            return target != null && SerializationUtility.HasManagedReferencesWithMissingTypes(target);
        }

        private static void InsertMissingTypeDetails(Object target, List<QuestDiagnostic> diagnostics)
        {
            if (!HasMissingTypes(target))
            {
                return;
            }

            foreach (var missing in SerializationUtility.GetManagedReferencesWithMissingTypes(target))
            {
                var typeName = string.IsNullOrEmpty(missing.namespaceName) ? missing.className : missing.namespaceName + "." + missing.className;
                diagnostics.Insert(0, new QuestDiagnostic(
                    QuestDiagnosticSeverity.Error,
                    QuestDiagnosticCodes.MissingNodeType,
                    $"Node class not found: {typeName} (assembly {missing.assemblyName}). Restore the class or add [MovedFrom]."));
            }
        }
    }
}
