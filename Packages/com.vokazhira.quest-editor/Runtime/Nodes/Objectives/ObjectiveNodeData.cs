using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Цель с прогрессом. Ждёт событий игры, пока прогресс меньше <see cref="RequiredCount"/>.
    /// </summary>
    [Serializable]
    public abstract class ObjectiveNodeData : QuestNodeData
    {
        private static readonly IReadOnlyList<QuestPortDefinition> PortList = Array.AsReadOnly(new[]
        {
            QuestPortDefinition.FlowInput(QuestPortIds.In, "Вход"),
            QuestPortDefinition.FlowOutput(QuestPortIds.Out, "Выполнено")
        });

        [SerializeField, Tooltip("Стабильный идентификатор цели. По нему сохраняется прогресс игрока.")]
        private string objectiveId;

        [SerializeField, Tooltip("Название цели для UI.")]
        private string title = string.Empty;

        [SerializeField, TextArea, Tooltip("Описание цели для UI.")]
        private string description = string.Empty;

        [SerializeField, Tooltip("Необязательная цель. В текущей версии только хранится.")]
        private bool isOptional;

        protected ObjectiveNodeData()
        {
            objectiveId = Guid.NewGuid().ToString("N");
        }

        public string ObjectiveId
        {
            get => objectiveId;
            set => objectiveId = value;
        }

        public string Title
        {
            get => title;
            set => title = value;
        }

        public string Description
        {
            get => description;
            set => description = value;
        }

        public bool IsOptional
        {
            get => isOptional;
            set => isOptional = value;
        }

        public abstract int RequiredCount { get; }

        public override IReadOnlyList<QuestPortDefinition> Ports => PortList;

        /// <summary>
        /// Чистая функция: сообщает, продвигает ли событие эту цель и на сколько.
        /// </summary>
        public abstract bool TryGetProgress(in QuestEvent e, out int delta);
    }
}
