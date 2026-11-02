using System;
using System.Collections.Generic;
using UnityEditor;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Типы узлов, которые можно создать в редакторе: неабстрактные, не generic наследники
    /// <see cref="QuestNodeData"/> с атрибутом <see cref="QuestNodeAttribute"/> и публичным конструктором без параметров.
    /// Список строится через <see cref="TypeCache"/> один раз за domain reload.
    /// </summary>
    internal static class QuestNodeTypeCache
    {
        private static List<QuestNodeTypeInfo> creatableTypes;
        private static Dictionary<Type, QuestNodeTypeInfo> infoByType;

        public static IReadOnlyList<QuestNodeTypeInfo> CreatableTypes
        {
            get
            {
                EnsureBuilt();
                return creatableTypes;
            }
        }

        /// <summary>
        /// Описание для любого типа узла, в том числе без атрибута (тогда цвет по умолчанию).
        /// </summary>
        public static QuestNodeTypeInfo GetInfo(Type type)
        {
            EnsureBuilt();
            if (type == null)
            {
                return null;
            }

            if (!infoByType.TryGetValue(type, out var info))
            {
                info = new QuestNodeTypeInfo(type, GetAttribute(type));
                infoByType.Add(type, info);
            }

            return info;
        }

        public static bool IsCreatable(Type type)
        {
            return type != null
                && typeof(QuestNodeData).IsAssignableFrom(type)
                && !type.IsAbstract
                && !type.IsGenericType
                && type.GetConstructor(Type.EmptyTypes) != null
                && GetAttribute(type) != null;
        }

        private static void EnsureBuilt()
        {
            if (creatableTypes != null)
            {
                return;
            }

            creatableTypes = new List<QuestNodeTypeInfo>();
            infoByType = new Dictionary<Type, QuestNodeTypeInfo>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<QuestNodeData>())
            {
                if (!IsCreatable(type))
                {
                    continue;
                }

                var info = new QuestNodeTypeInfo(type, GetAttribute(type));
                creatableTypes.Add(info);
                infoByType[type] = info;
            }

            creatableTypes.Sort((a, b) => string.CompareOrdinal(a.MenuPath, b.MenuPath));
        }

        private static QuestNodeAttribute GetAttribute(Type type)
        {
            return (QuestNodeAttribute)Attribute.GetCustomAttribute(type, typeof(QuestNodeAttribute), false);
        }
    }
}
