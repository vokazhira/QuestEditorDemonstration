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
            QuestPortDefinition.FlowInput(QuestPortIds.In, "In"),
            QuestPortDefinition.FlowOutput(QuestPortIds.Out, "Done")
        });

        [SerializeField, Tooltip("Stable objective identifier. Player progress is saved under it.")]
        private string objectiveId;

        [SerializeField, Tooltip("Objective title shown in the UI.")]
        private string title = string.Empty;

        [SerializeField, TextArea, Tooltip("Objective description shown in the UI.")]
        private string description = string.Empty;

        [SerializeField, Tooltip("Optional objective. Stored only in this version.")]
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

        public override void Validate(QuestValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                context.AddError(QuestDiagnosticCodes.InvalidValue, "ObjectiveId is empty.");
            }

            if (RequiredCount <= 0)
            {
                context.AddError(QuestDiagnosticCodes.InvalidValue, $"Required count must be greater than 0 (currently {RequiredCount}).");
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                context.AddWarning(QuestDiagnosticCodes.ObjectiveWithoutDescription, "The objective has no description for the UI.");
            }
        }

        /// <summary>
        /// Чистая функция: сообщает, продвигает ли событие эту цель и на сколько.
        /// </summary>
        public abstract bool TryGetProgress(in QuestEvent e, out int delta);
    }
}
