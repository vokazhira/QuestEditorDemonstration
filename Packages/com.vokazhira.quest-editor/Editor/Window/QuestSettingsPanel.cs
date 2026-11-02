using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Настройки задания, привязанные к SerializedObject рабочей копии. Изменения идут через
    /// SerializedObject, поэтому попадают в Undo.
    /// </summary>
    internal sealed class QuestSettingsPanel : VisualElement
    {
        public QuestSettingsPanel()
        {
            AddToClassList("qe-settings");
            AddField("questId", "Quest Id");
            AddField("displayName", "Название");
            AddField("description", "Описание");
            AddField("rewardMode", "Выдача наград");
        }

        public event Action Changed;

        public void Bind(SerializedObject serializedGraph)
        {
            if (serializedGraph == null)
            {
                this.Unbind();
                return;
            }

            BindingExtensions.Bind(this, serializedGraph);
        }

        private void AddField(string propertyPath, string label)
        {
            var field = new PropertyField { bindingPath = propertyPath, label = label };
            field.RegisterValueChangeCallback(_ => Changed?.Invoke());
            Add(field);
        }
    }
}
