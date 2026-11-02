using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Нижняя панель окна: результаты валидации. Клик по строке сообщает о выбранной диагностике.
    /// </summary>
    internal sealed class QuestDiagnosticsView : VisualElement
    {
        private readonly List<QuestDiagnostic> diagnostics = new List<QuestDiagnostic>();
        private readonly Label summary;
        private readonly Label staleLabel;
        private readonly ListView listView;
        private bool hasResults;

        public QuestDiagnosticsView()
        {
            AddToClassList("qe-diagnostics");

            var header = new VisualElement();
            header.AddToClassList("qe-diagnostics__header");
            summary = new Label();
            summary.AddToClassList("qe-diagnostics__summary");
            staleLabel = new Label("The graph has changed; results may be outdated. Press Validate.");
            staleLabel.AddToClassList("qe-diagnostics__stale");
            header.Add(summary);
            header.Add(staleLabel);
            Add(header);

            listView = new ListView
            {
                itemsSource = diagnostics,
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                makeItem = MakeItem,
                bindItem = BindItem
            };
            listView.AddToClassList("qe-diagnostics__list");
            listView.selectionChanged += OnSelectionChanged;
            Add(listView);

            ResetResults();
        }

        public event Action<QuestDiagnostic> DiagnosticSelected;

        public void ResetResults()
        {
            hasResults = false;
            diagnostics.Clear();
            listView.RefreshItems();
            summary.text = "Validation has not run yet.";
            staleLabel.style.display = DisplayStyle.None;
        }

        public void SetDiagnostics(IReadOnlyList<QuestDiagnostic> results)
        {
            hasResults = true;
            diagnostics.Clear();
            diagnostics.AddRange(results);
            listView.ClearSelection();
            listView.RefreshItems();

            var errors = diagnostics.Count(d => d.Severity == QuestDiagnosticSeverity.Error);
            var warnings = diagnostics.Count(d => d.Severity == QuestDiagnosticSeverity.Warning);
            summary.text = errors == 0 && warnings == 0
                ? "No errors or warnings."
                : $"Errors: {errors}, warnings: {warnings}." + (errors > 0 ? " The quest cannot run in the game." : string.Empty);
            summary.EnableInClassList("qe-diagnostics__summary--error", errors > 0);
            staleLabel.style.display = DisplayStyle.None;
        }

        public void SetStale(bool stale)
        {
            staleLabel.style.display = hasResults && stale ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static Texture GetIcon(QuestDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case QuestDiagnosticSeverity.Error:
                    return EditorGUIUtility.IconContent("console.erroricon.sml").image;
                case QuestDiagnosticSeverity.Warning:
                    return EditorGUIUtility.IconContent("console.warnicon.sml").image;
                default:
                    return EditorGUIUtility.IconContent("console.infoicon.sml").image;
            }
        }

        private static VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("qe-diagnostic");
            var icon = new Image { name = "icon" };
            icon.AddToClassList("qe-diagnostic__icon");
            var code = new Label { name = "code" };
            code.AddToClassList("qe-diagnostic__code");
            var message = new Label { name = "message" };
            message.AddToClassList("qe-diagnostic__message");
            row.Add(icon);
            row.Add(code);
            row.Add(message);
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            var diagnostic = diagnostics[index];
            element.Q<Image>("icon").image = GetIcon(diagnostic.Severity);
            element.Q<Label>("code").text = diagnostic.Code;
            element.Q<Label>("message").text = diagnostic.Message;
            element.tooltip = diagnostic.NodeId == null ? diagnostic.Message : diagnostic.Message + "\nClick to select the node.";
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            var selected = selection.FirstOrDefault();
            if (selected is QuestDiagnostic diagnostic)
            {
                DiagnosticSelected?.Invoke(diagnostic);
            }
        }
    }
}
