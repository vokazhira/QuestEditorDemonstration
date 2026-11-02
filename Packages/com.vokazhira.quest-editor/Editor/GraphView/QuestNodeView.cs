using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Универсальное представление узла любого типа. Хранит только Id узла; порты, заголовок и цвет
    /// берутся из модели и пересобираются из неё в любой момент.
    /// </summary>
    internal sealed class QuestNodeView : Node
    {
        private static readonly Color RewardPortColor = new Color(0.98f, 0.8f, 0.25f);

        public QuestNodeView(QuestNodeData data)
        {
            NodeId = data.Id;
            viewDataKey = data.Id;
            AddToClassList("qe-node");

            capabilities &= ~(Capabilities.Copiable | Capabilities.Renamable);

            var info = QuestNodeTypeCache.GetInfo(data.GetType());
            var color = info?.Color ?? QuestNodeTypeInfo.DefaultColor;
            titleContainer.style.backgroundColor = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f);
            titleContainer.style.borderBottomColor = color;
            titleContainer.style.borderBottomWidth = 2;
            tooltip = info?.MenuPath;

            foreach (var definition in data.Ports)
            {
                var port = InstantiatePort(
                    Orientation.Horizontal,
                    definition.Direction == QuestPortDirection.Input ? Direction.Input : Direction.Output,
                    definition.Capacity == QuestPortCapacity.Single ? Port.Capacity.Single : Port.Capacity.Multi,
                    typeof(QuestNodeData));
                port.portName = definition.DisplayName;
                port.userData = definition.PortId;
                port.tooltip = definition.Kind == QuestPortKind.Reward ? "Reward" : "Flow";
                if (definition.Kind == QuestPortKind.Reward)
                {
                    port.portColor = RewardPortColor;
                }

                if (definition.Direction == QuestPortDirection.Input)
                {
                    inputContainer.Add(port);
                }
                else
                {
                    outputContainer.Add(port);
                }
            }

            Refresh(data);
            RefreshExpandedState();
            RefreshPorts();
        }

        public string NodeId { get; }

        public IEnumerable<Port> AllPorts
        {
            get
            {
                foreach (var element in inputContainer.Children())
                {
                    if (element is Port port)
                    {
                        yield return port;
                    }
                }

                foreach (var element in outputContainer.Children())
                {
                    if (element is Port port)
                    {
                        yield return port;
                    }
                }
            }
        }

        public void Refresh(QuestNodeData data)
        {
            title = data.GetSummary();
            SetPosition(new Rect(data.Position, Vector2.zero));
        }

        public Port FindPort(string portId)
        {
            foreach (var port in AllPorts)
            {
                if ((string)port.userData == portId)
                {
                    return port;
                }
            }

            return null;
        }
    }
}
