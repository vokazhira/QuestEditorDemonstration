using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Левая панель окна: список всех заданий проекта с поиском и операциями над ассетами.
    /// Сама ассеты не меняет — только сообщает окну о запросах пользователя.
    /// </summary>
    internal sealed class QuestLibraryView : VisualElement
    {
        private readonly List<QuestGraphAsset> allQuests = new List<QuestGraphAsset>();
        private readonly List<QuestGraphAsset> visibleQuests = new List<QuestGraphAsset>();
        private readonly Dictionary<QuestGraphAsset, string> firstErrorByQuest = new Dictionary<QuestGraphAsset, string>();
        private readonly ToolbarSearchField searchField;
        private readonly ListView listView;
        private readonly Button duplicateButton;
        private readonly Button deleteButton;
        private string currentGuid;

        public QuestLibraryView()
        {
            AddToClassList("qe-library");

            var header = new Label("Quests");
            header.AddToClassList("qe-library__header");
            Add(header);

            searchField = new ToolbarSearchField();
            searchField.AddToClassList("qe-library__search");
            searchField.tooltip = "Search by name or questId";
            searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            Add(searchField);

            listView = new ListView
            {
                itemsSource = visibleQuests,
                fixedItemHeight = 36,
                selectionType = SelectionType.Single,
                makeItem = MakeItem,
                bindItem = BindItem
            };
            listView.AddToClassList("qe-library__list");
            listView.itemsChosen += OnItemsChosen;
            listView.selectionChanged += _ => UpdateButtons();
            Add(listView);

            var buttons = new VisualElement();
            buttons.AddToClassList("qe-library__buttons");
            buttons.Add(new Button(Refresh) { text = "Refresh", tooltip = "Find all quests in the project again" });
            buttons.Add(new Button(() => CreateRequested?.Invoke()) { text = "Create", tooltip = "Create a new quest asset" });
            duplicateButton = new Button(() => RaiseForSelection(DuplicateRequested)) { text = "Duplicate", tooltip = "Duplicate the selected quest with a new questId" };
            deleteButton = new Button(() => RaiseForSelection(DeleteRequested)) { text = "Delete", tooltip = "Delete the selected quest (asks for confirmation)" };
            buttons.Add(duplicateButton);
            buttons.Add(deleteButton);
            Add(buttons);

            UpdateButtons();
        }

        public event Action<QuestGraphAsset> OpenRequested;
        public event Action CreateRequested;
        public event Action<QuestGraphAsset> DuplicateRequested;
        public event Action<QuestGraphAsset> DeleteRequested;

        public void Refresh()
        {
            allQuests.Clear();
            allQuests.AddRange(QuestAssetUtility.FindAllQuestAssets());
            UpdateValidity();
            ApplyFilter();
        }

        public void SetCurrent(string assetGuid)
        {
            currentGuid = assetGuid;
            listView.RefreshItems();
        }

        public void Select(QuestGraphAsset asset)
        {
            var index = visibleQuests.IndexOf(asset);
            if (index >= 0)
            {
                listView.SetSelection(index);
                listView.ScrollToItem(index);
            }
            else
            {
                listView.ClearSelection();
            }
        }

        /// <summary>
        /// Проверяет сохранённые ассеты (не рабочую копию) и запоминает первую ошибку каждого невалидного задания.
        /// </summary>
        private void UpdateValidity()
        {
            firstErrorByQuest.Clear();
            foreach (var quest in allQuests)
            {
                if (quest == null)
                {
                    continue;
                }

                var diagnostics = QuestEditorValidation.Validate(quest, quest, allQuests);
                var error = diagnostics.FirstOrDefault(d => d.Severity == QuestDiagnosticSeverity.Error);
                if (error.Severity == QuestDiagnosticSeverity.Error && !string.IsNullOrEmpty(error.Code))
                {
                    firstErrorByQuest[quest] = $"{error.Code}: {error.Message}";
                }
            }
        }

        private void ApplyFilter()
        {
            var selected = listView.selectedItem as QuestGraphAsset;
            var filter = searchField.value?.Trim() ?? string.Empty;

            visibleQuests.Clear();
            visibleQuests.AddRange(allQuests.Where(q => q != null && Matches(q, filter)));
            listView.RefreshItems();

            if (selected != null)
            {
                Select(selected);
            }

            UpdateButtons();
        }

        private static bool Matches(QuestGraphAsset quest, string filter)
        {
            if (filter.Length == 0)
            {
                return true;
            }

            return Contains(quest.name, filter) || Contains(quest.DisplayName, filter) || Contains(quest.QuestId, filter);
        }

        private static bool Contains(string text, string filter)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static VisualElement MakeItem()
        {
            var item = new VisualElement();
            item.AddToClassList("qe-library-item");
            var icon = new Image { name = "error-icon", image = QuestDiagnosticsView.GetIcon(QuestDiagnosticSeverity.Error) };
            icon.AddToClassList("qe-library-item__icon");
            var texts = new VisualElement();
            texts.AddToClassList("qe-library-item__texts");
            var title = new Label { name = "title" };
            title.AddToClassList("qe-library-item__title");
            var questId = new Label { name = "quest-id" };
            questId.AddToClassList("qe-library-item__id");
            texts.Add(title);
            texts.Add(questId);
            item.Add(icon);
            item.Add(texts);
            return item;
        }

        private void BindItem(VisualElement element, int index)
        {
            var quest = index >= 0 && index < visibleQuests.Count ? visibleQuests[index] : null;
            var title = element.Q<Label>("title");
            var questId = element.Q<Label>("quest-id");
            var icon = element.Q<Image>("error-icon");
            var hasError = quest != null && firstErrorByQuest.ContainsKey(quest);
            icon.style.visibility = hasError ? Visibility.Visible : Visibility.Hidden;
            element.tooltip = hasError ? "This quest is invalid and will not run in the game.\n" + firstErrorByQuest[quest] : null;
            if (quest == null)
            {
                title.text = "(missing)";
                questId.text = string.Empty;
                return;
            }

            title.text = string.IsNullOrEmpty(quest.DisplayName) ? quest.name : $"{quest.DisplayName}  ·  {quest.name}";
            questId.text = string.IsNullOrEmpty(quest.QuestId) ? "no questId" : quest.QuestId;

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(quest));
            element.EnableInClassList("qe-library-item--current", !string.IsNullOrEmpty(currentGuid) && guid == currentGuid);
        }

        private void OnItemsChosen(IEnumerable<object> items)
        {
            if (items.FirstOrDefault() is QuestGraphAsset quest)
            {
                OpenRequested?.Invoke(quest);
            }
        }

        private void RaiseForSelection(Action<QuestGraphAsset> handler)
        {
            if (listView.selectedItem is QuestGraphAsset quest)
            {
                handler?.Invoke(quest);
            }
        }

        private void UpdateButtons()
        {
            var hasSelection = listView.selectedItem is QuestGraphAsset;
            duplicateButton.SetEnabled(hasSelection);
            deleteButton.SetEnabled(hasSelection);
        }
    }
}
