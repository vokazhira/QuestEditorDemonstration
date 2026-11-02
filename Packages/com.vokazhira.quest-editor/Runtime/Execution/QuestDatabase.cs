using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Явный список заданий игры. <see cref="QuestManager"/> регистрирует задания только отсюда.
    /// В редакторе список можно собрать кнопкой «Собрать все задания проекта».
    /// </summary>
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "Quest Editor/Quest Database", order = 1)]
    public sealed class QuestDatabase : ScriptableObject
    {
        [SerializeField, Tooltip("Quests available in the game.")]
        private List<QuestGraphAsset> quests = new List<QuestGraphAsset>();

        public IReadOnlyList<QuestGraphAsset> Quests => quests;

        internal void SetQuests(IEnumerable<QuestGraphAsset> value)
        {
            quests.Clear();
            quests.AddRange(value);
        }
    }
}
