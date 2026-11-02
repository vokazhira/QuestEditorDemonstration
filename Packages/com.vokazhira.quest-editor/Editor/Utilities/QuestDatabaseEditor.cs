using UnityEditor;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Инспектор <see cref="QuestDatabase"/>: стандартный список и кнопка «Собрать все задания проекта».
    /// Изменение идёт через SerializedObject, поэтому отменяется Ctrl+Z.
    /// </summary>
    [CustomEditor(typeof(QuestDatabase))]
    internal sealed class QuestDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("Собрать все задания проекта", "Заменить список всеми QuestGraphAsset из проекта")))
            {
                CollectAllQuests();
            }

            var database = (QuestDatabase)target;
            var invalid = 0;
            foreach (var quest in database.Quests)
            {
                if (quest == null || QuestGraphValidator.HasErrors(QuestEditorValidation.Validate(quest, quest, database.Quests)))
                {
                    invalid++;
                }
            }

            if (invalid > 0)
            {
                EditorGUILayout.HelpBox($"Заданий с ошибками или пустых ссылок: {invalid}. Такие задания не будут запускаться в игре.", MessageType.Warning);
            }
        }

        private void CollectAllQuests()
        {
            var quests = QuestAssetUtility.FindAllQuestAssets();
            serializedObject.Update();
            var list = serializedObject.FindProperty("quests");
            list.arraySize = quests.Count;
            for (var i = 0; i < quests.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
