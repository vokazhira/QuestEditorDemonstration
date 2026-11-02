using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Верхняя панель окна: имя задания, индикатор несохранённых изменений и команды.
    /// </summary>
    internal sealed class QuestEditorToolbar : Toolbar
    {
        private readonly Label titleLabel;
        private readonly ToolbarButton saveButton;
        private readonly ToolbarButton saveAsButton;
        private readonly ToolbarButton validateButton;
        private readonly ToolbarToggle settingsToggle;

        public QuestEditorToolbar()
        {
            AddToClassList("qe-toolbar");

            Add(new ToolbarButton(() => NewClicked?.Invoke()) { text = "New", tooltip = "New quest (unsaved draft)" });
            Add(new ToolbarButton(() => OpenClicked?.Invoke()) { text = "Open", tooltip = "Open a quest asset" });
            saveButton = new ToolbarButton(() => SaveClicked?.Invoke()) { text = "Save", tooltip = "Save the quest to its asset" };
            saveAsButton = new ToolbarButton(() => SaveAsClicked?.Invoke()) { text = "Save As", tooltip = "Save the quest to a new asset" };
            Add(saveButton);
            Add(saveAsButton);
            validateButton = new ToolbarButton(() => ValidateClicked?.Invoke()) { text = "Validate", tooltip = "Check the graph for errors" };
            Add(validateButton);

            settingsToggle = new ToolbarToggle { text = "Settings", tooltip = "Show quest settings: questId, name, description, reward mode" };
            settingsToggle.RegisterValueChangedCallback(e => SettingsToggled?.Invoke(e.newValue));
            Add(settingsToggle);

            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1;
            Add(spacer);

            titleLabel = new Label();
            titleLabel.AddToClassList("qe-toolbar__title");
            Add(titleLabel);
        }

        public event Action NewClicked;
        public event Action OpenClicked;
        public event Action SaveClicked;
        public event Action SaveAsClicked;
        public event Action ValidateClicked;
        public event Action<bool> SettingsToggled;

        public void SetState(string title, bool hasUnsavedChanges, bool hasQuest, bool settingsVisible)
        {
            titleLabel.text = hasUnsavedChanges ? title + " *" : title;
            titleLabel.EnableInClassList("qe-toolbar__title--dirty", hasUnsavedChanges);
            saveButton.SetEnabled(hasQuest);
            saveAsButton.SetEnabled(hasQuest);
            validateButton.SetEnabled(hasQuest);
            settingsToggle.SetEnabled(hasQuest);
            settingsToggle.SetValueWithoutNotify(settingsVisible);
        }
    }
}
