using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Базовый класс данных узла графа. Хранится в <see cref="QuestGraphAsset"/> через [SerializeReference].
    /// При переименовании или переносе наследника добавляйте [MovedFrom], иначе сохранённые данные потеряются.
    /// </summary>
    [Serializable]
    public abstract class QuestNodeData
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField, HideInInspector] private Vector2 position;

        protected QuestNodeData()
        {
            id = Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Уникальный в пределах графа идентификатор. Генерируется при создании и не меняется.
        /// </summary>
        public string Id => id;

        public Vector2 Position
        {
            get => position;
            set => position = value;
        }

        public abstract IReadOnlyList<QuestPortDefinition> Ports { get; }

        /// <summary>
        /// Проверки собственных полей узла. Вызывается валидатором; диагностики добавляются через <paramref name="context"/>.
        /// </summary>
        public virtual void Validate(QuestValidationContext context)
        {
        }

        /// <summary>
        /// Короткий текст для заголовка узла в редакторе.
        /// </summary>
        public virtual string GetSummary()
        {
            return GetType().Name;
        }

        public QuestPortDefinition FindPort(string portId)
        {
            var ports = Ports;
            if (ports == null)
            {
                return null;
            }

            for (var i = 0; i < ports.Count; i++)
            {
                if (string.Equals(ports[i].PortId, portId, StringComparison.Ordinal))
                {
                    return ports[i];
                }
            }

            return null;
        }
    }
}
