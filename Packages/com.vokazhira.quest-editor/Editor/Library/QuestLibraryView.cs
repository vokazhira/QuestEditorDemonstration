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
        private readonly ToolbarSearchField searchField;
        private readonly ListView listView;
        private readonly Button duplicateButton;
        private readonly Button deleteButton;
        private string currentGuid;

        public QuestLibraryView()
        {
            AddToClassList("qe-library");

            var header = new Label("Задания");
            header.AddToClassList("qe-library__header");
            Add(header);

            searchField = new ToolbarSearchField();
            searchField.AddToClassList("qe-library__search");
            searchField.tooltip = "Поиск по названию и questId";
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
            buttons.Add(new Button(Refresh) { text = "Обновить", tooltip = "Заново найти все задания проекта" });
            buttons.Add(new Button(() => CreateRequested?.Invoke()) { text = "Создать", tooltip = "Создать новый ассет задания" });
            duplicateButton = new Button(() => RaiseForSelection(DuplicateRequested)) { text = "Копия", tooltip = "Дублировать выбранное задание с новым questId" };
            deleteButton = new Button(() => RaiseForSelection(DeleteRequested)) { text = "Удалить", tooltip = "Удалить выбранное задание (с подтверждением)" };
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
            var title = new Label { name = "title" };
            title.AddToClassList("qe-library-item__title");
            var questId = new Label { name = "quest-id" };
            questId.AddToClassList("qe-library-item__id");
            item.Add(title);
            item.Add(questId);
            return item;
        }

        private void BindItem(VisualElement element, int index)
        {
            var quest = index >= 0 && index < visibleQuests.Count ? visibleQuests[index] : null;
            var title = element.Q<Label>("title");
            var questId = element.Q<Label>("quest-id");
            if (quest == null)
            {
                title.text = "(отсутствует)";
                questId.text = string.Empty;
                return;
            }

            title.text = string.IsNullOrEmpty(quest.DisplayName) ? quest.name : $"{quest.DisplayName}  ·  {quest.name}";
            questId.text = string.IsNullOrEmpty(quest.QuestId) ? "questId не задан" : quest.QuestId;

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
