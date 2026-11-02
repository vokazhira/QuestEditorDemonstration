using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Помечает конкретный тип узла для меню создания в редакторе.
    /// <paramref name="menuPath"/> — путь в меню вида «Категория/Имя»,
    /// <paramref name="colorHex"/> — цвет заголовка в формате HTML (#RRGGBB).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class QuestNodeAttribute : Attribute
    {
        public QuestNodeAttribute(string menuPath, string colorHex = null)
        {
            MenuPath = menuPath ?? string.Empty;
            ColorHex = colorHex;
        }

        public string MenuPath { get; }
        public string ColorHex { get; }

        public bool TryGetColor(out Color color)
        {
            color = default;
            return !string.IsNullOrEmpty(ColorHex) && ColorUtility.TryParseHtmlString(ColorHex, out color);
        }
    }
}
