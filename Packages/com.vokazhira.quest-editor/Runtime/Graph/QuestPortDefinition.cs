using System;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Неизменяемое описание порта узла. Не сериализуется: узел вычисляет набор портов сам.
    /// </summary>
    public sealed class QuestPortDefinition
    {
        public QuestPortDefinition(
            string portId,
            QuestPortDirection direction,
            QuestPortKind kind,
            QuestPortCapacity capacity,
            string displayName)
        {
            if (string.IsNullOrEmpty(portId))
            {
                throw new ArgumentException("Port id must not be empty.", nameof(portId));
            }

            PortId = portId;
            Direction = direction;
            Kind = kind;
            Capacity = capacity;
            DisplayName = displayName ?? string.Empty;
        }

        public string PortId { get; }
        public QuestPortDirection Direction { get; }
        public QuestPortKind Kind { get; }
        public QuestPortCapacity Capacity { get; }
        public string DisplayName { get; }

        public static QuestPortDefinition FlowInput(string portId, string displayName)
        {
            return new QuestPortDefinition(portId, QuestPortDirection.Input, QuestPortKind.Flow, QuestPortCapacity.Multi, displayName);
        }

        public static QuestPortDefinition FlowOutput(string portId, string displayName)
        {
            return new QuestPortDefinition(portId, QuestPortDirection.Output, QuestPortKind.Flow, QuestPortCapacity.Single, displayName);
        }

        public override string ToString()
        {
            return $"{PortId} ({Direction}, {Kind}, {Capacity})";
        }
    }
}
