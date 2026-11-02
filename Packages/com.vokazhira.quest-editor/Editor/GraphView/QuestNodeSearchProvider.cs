using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Список типов узлов для <see cref="SearchWindow"/>. Выбранный тип и экранная позиция курсора
    /// передаются в <see cref="NodeTypeSelected"/>.
    /// </summary>
    internal sealed class QuestNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public Action<Type, Vector2> NodeTypeSelected;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            return BuildTree(QuestNodeTypeCache.CreatableTypes);
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (!(searchTreeEntry.userData is Type type))
            {
                return false;
            }

            NodeTypeSelected?.Invoke(type, context.screenMousePosition);
            return true;
        }

        /// <summary>
        /// Строит дерево поиска из путей вида «Категория/Имя»: группы для каждой категории, листья — типы узлов.
        /// </summary>
        public static List<SearchTreeEntry> BuildTree(IEnumerable<QuestNodeTypeInfo> types)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Создать узел"), 0)
            };

            var sorted = new List<QuestNodeTypeInfo>(types);
            sorted.Sort((a, b) => string.CompareOrdinal(a.MenuPath, b.MenuPath));

            var openGroups = new List<string>();
            foreach (var info in sorted)
            {
                var segments = info.MenuPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                {
                    continue;
                }

                var groupDepth = segments.Length - 1;
                var common = 0;
                while (common < groupDepth && common < openGroups.Count && openGroups[common] == segments[common])
                {
                    common++;
                }

                openGroups.RemoveRange(common, openGroups.Count - common);
                for (var level = common; level < groupDepth; level++)
                {
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(segments[level]), level + 1));
                    openGroups.Add(segments[level]);
                }

                tree.Add(new SearchTreeEntry(new GUIContent(segments[segments.Length - 1]))
                {
                    level = groupDepth + 1,
                    userData = info.Type
                });
            }

            return tree;
        }
    }
}
