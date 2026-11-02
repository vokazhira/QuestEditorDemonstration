using System;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Описание типа узла, доступного для создания: тип, путь в меню и цвет из <see cref="QuestNodeAttribute"/>.
    /// </summary>
    internal sealed class QuestNodeTypeInfo
    {
        public static readonly Color DefaultColor = new Color(0.35f, 0.35f, 0.35f);

        public QuestNodeTypeInfo(Type type, QuestNodeAttribute attribute)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            MenuPath = string.IsNullOrEmpty(attribute?.MenuPath) ? type.Name : attribute.MenuPath;
            Color = attribute != null && attribute.TryGetColor(out var color) ? color : DefaultColor;

            var separator = MenuPath.LastIndexOf('/');
            DisplayName = separator >= 0 ? MenuPath.Substring(separator + 1) : MenuPath;
        }

        public Type Type { get; }
        public string MenuPath { get; }
        public string DisplayName { get; }
        public Color Color { get; }
    }
}
