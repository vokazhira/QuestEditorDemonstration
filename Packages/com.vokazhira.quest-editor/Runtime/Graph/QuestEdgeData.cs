using System;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Связь между портами двух узлов. Хранит только идентификаторы.
    /// </summary>
    [Serializable]
    public sealed class QuestEdgeData
    {
        [SerializeField] private string fromNodeId;
        [SerializeField] private string fromPortId;
        [SerializeField] private string toNodeId;
        [SerializeField] private string toPortId;

        public QuestEdgeData()
        {
        }

        public QuestEdgeData(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            this.fromNodeId = fromNodeId;
            this.fromPortId = fromPortId;
            this.toNodeId = toNodeId;
            this.toPortId = toPortId;
        }

        public string FromNodeId => fromNodeId;
        public string FromPortId => fromPortId;
        public string ToNodeId => toNodeId;
        public string ToPortId => toPortId;

        public bool Matches(string fromNode, string fromPort, string toNode, string toPort)
        {
            return string.Equals(fromNodeId, fromNode, StringComparison.Ordinal)
                && string.Equals(fromPortId, fromPort, StringComparison.Ordinal)
                && string.Equals(toNodeId, toNode, StringComparison.Ordinal)
                && string.Equals(toPortId, toPort, StringComparison.Ordinal);
        }

        public bool Touches(string nodeId)
        {
            return string.Equals(fromNodeId, nodeId, StringComparison.Ordinal)
                || string.Equals(toNodeId, nodeId, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return $"{fromNodeId}.{fromPortId} -> {toNodeId}.{toPortId}";
        }
    }
}
