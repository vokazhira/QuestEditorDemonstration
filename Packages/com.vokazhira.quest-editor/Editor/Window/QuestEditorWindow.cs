using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Окно Tools → Quest Editor. Хранит рабочую копию открытого задания в сериализуемом поле,
    /// поэтому переживает domain reload; UI полностью пересобирается в <see cref="CreateGUI"/>.
    /// </summary>
    internal sealed class QuestEditorWindow : EditorWindow
    {
        private const string StyleSheetPath = "Packages/com.vokazhira.quest-editor/Editor/Window/QuestEditor.uss";
        private const string NewQuestId = "new_quest";

        [SerializeField] private QuestWorkingCopy session;
        [SerializeField] private bool settingsVisible;
        [SerializeField] private bool hasStoredView;
        [SerializeField] private Vector3 viewPosition;
        [SerializeField] private Vector3 viewScale = Vector3.one;

        private QuestEditorToolbar toolbar;
        private QuestSettingsPanel settingsPanel;
        private QuestLibraryView library;
        private Label graphPlaceholder;
        private QuestGraphView graphView;
        private QuestNodeInspector inspector;
        private QuestDiagnosticsView diagnosticsView;
        private string validatedSnapshot;
        private SerializedObject serializedGraph;

        private bool HasOpenQuest => session != null && session.IsAlive;

        [MenuItem("Tools/Quest Editor")]
        private static void OpenFromMenu()
        {
            ShowWindow();
        }

        public static QuestEditorWindow ShowWindow()
        {
            var window = GetWindow<QuestEditorWindow>();
            window.Show();
            window.Focus();
            return window;
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceId) is QuestGraphAsset asset))
            {
                return false;
            }

            ShowWindow().OpenAsset(asset);
            return true;
        }

        public void OpenAsset(QuestGraphAsset asset)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset))
            {
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (HasOpenQuest && session.AssetGuid == guid)
            {
                library?.Select(asset);
                return;
            }

            if (!ConfirmCloseCurrent())
            {
                return;
            }

            ReplaceSession(QuestWorkingCopy.Open(asset));
            library?.Select(asset);
        }

        public override void SaveChanges()
        {
            if (SaveCurrent())
            {
                base.SaveChanges();
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Quest Editor");
            saveChangesMessage = "В задании есть несохранённые изменения. Сохранить их перед закрытием?";

            RestoreSessionAfterReload();

            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            QuestLibraryPostprocessor.QuestAssetsChanged += OnQuestAssetsChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            QuestLibraryPostprocessor.QuestAssetsChanged -= OnQuestAssetsChanged;
            DisposeSerializedGraph();
        }

        private void OnDestroy()
        {
            session?.Dispose();
            session = null;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"Quest Editor: stylesheet not found at '{StyleSheetPath}'.");
            }

            root.AddToClassList("qe-root");

            toolbar = new QuestEditorToolbar();
            toolbar.NewClicked += NewQuest;
            toolbar.OpenClicked += OpenQuestFromFile;
            toolbar.SaveClicked += () => SaveCurrent();
            toolbar.SaveAsClicked += () => SaveCurrentAs();
            toolbar.ValidateClicked += () => RunValidation();
            toolbar.SettingsToggled += visible =>
            {
                settingsVisible = visible;
                RefreshView();
            };
            root.Add(toolbar);

            settingsPanel = new QuestSettingsPanel();
            settingsPanel.Changed += UpdateUnsavedState;
            root.Add(settingsPanel);

            var body = new VisualElement();
            body.AddToClassList("qe-body");
            root.Add(body);

            library = new QuestLibraryView();
            library.OpenRequested += OpenAsset;
            library.CreateRequested += CreateQuestAsset;
            library.DuplicateRequested += DuplicateQuestAsset;
            library.DeleteRequested += DeleteQuestAsset;
            body.Add(library);

            var center = new VisualElement();
            center.AddToClassList("qe-center");
            graphView = new QuestGraphView(this);
            graphView.ModelChanged += OnGraphModelChanged;
            graphView.SelectionChanged += () => RefreshInspector(false);
            graphView.viewTransformChanged = OnViewTransformChanged;
            center.Add(graphView);
            graphPlaceholder = new Label();
            graphPlaceholder.AddToClassList("qe-graph-placeholder");
            center.Add(graphPlaceholder);
            body.Add(center);

            inspector = new QuestNodeInspector();
            inspector.NodeChanged += OnInspectorNodeChanged;
            body.Add(inspector);

            diagnosticsView = new QuestDiagnosticsView();
            diagnosticsView.DiagnosticSelected += OnDiagnosticSelected;
            root.Add(diagnosticsView);

            library.Refresh();
            BindSession();
            RestoreOrFrameView();
        }

        private void RestoreSessionAfterReload()
        {
            if (session == null || session.IsAlive)
            {
                return;
            }

            var asset = session.LoadAsset();
            session = asset != null ? QuestWorkingCopy.Open(asset) : null;
        }

        private void ReplaceSession(QuestWorkingCopy next)
        {
            session?.Dispose();
            session = next;
            hasStoredView = false;
            BindSession();
            RestoreOrFrameView();
        }

        private void BindSession()
        {
            DisposeSerializedGraph();
            if (HasOpenQuest)
            {
                serializedGraph = new SerializedObject(session.Graph);
            }

            settingsPanel?.Bind(serializedGraph);
            graphView?.SetGraph(HasOpenQuest ? session.Graph : null);
            RefreshInspector(true);
            RunValidation();
            library?.SetCurrent(HasOpenQuest ? session.AssetGuid : null);
            UpdateUnsavedState();
        }

        private void DisposeSerializedGraph()
        {
            serializedGraph?.Dispose();
            serializedGraph = null;
        }

        private void UpdateUnsavedState()
        {
            hasUnsavedChanges = HasOpenQuest && session.RefreshUnsavedState();
            diagnosticsView?.SetStale(HasOpenQuest && validatedSnapshot != null && session.CurrentSnapshot != validatedSnapshot);
            RefreshView();
        }

        private List<QuestDiagnostic> RunValidation()
        {
            if (diagnosticsView == null)
            {
                return null;
            }

            if (!HasOpenQuest)
            {
                validatedSnapshot = null;
                diagnosticsView.ResetResults();
                return null;
            }

            var diagnostics = QuestEditorValidation.Validate(session.Graph, session.LoadAsset());
            session.RefreshUnsavedState();
            validatedSnapshot = session.CurrentSnapshot;
            diagnosticsView.SetDiagnostics(diagnostics);
            return diagnostics;
        }

        private void OnDiagnosticSelected(QuestDiagnostic diagnostic)
        {
            if (graphView == null || diagnostic.NodeId == null)
            {
                return;
            }

            graphView.SelectNode(diagnostic.NodeId);
            graphView.FrameSelection();
        }

        private void RefreshView()
        {
            if (toolbar == null)
            {
                return;
            }

            var hasQuest = HasOpenQuest;
            toolbar.SetState(hasQuest ? session.Title : "Нет открытого задания", hasUnsavedChanges, hasQuest, settingsVisible);
            settingsPanel.style.display = hasQuest && settingsVisible ? DisplayStyle.Flex : DisplayStyle.None;

            graphView.style.display = hasQuest ? DisplayStyle.Flex : DisplayStyle.None;
            inspector.style.display = hasQuest ? DisplayStyle.Flex : DisplayStyle.None;
            diagnosticsView.style.display = hasQuest ? DisplayStyle.Flex : DisplayStyle.None;
            graphPlaceholder.style.display = hasQuest ? DisplayStyle.None : DisplayStyle.Flex;
            graphPlaceholder.text = "Выберите задание в библиотеке (двойной клик) или создайте новое кнопкой New.";
        }

        private void OnUndoRedoPerformed()
        {
            if (!HasOpenQuest)
            {
                return;
            }

            session.Graph.InvalidateNodeIndex();
            serializedGraph?.Update();
            graphView?.Rebuild();
            RefreshInspector(true);
            UpdateUnsavedState();
        }

        private void OnGraphModelChanged()
        {
            serializedGraph?.Update();
            RefreshInspector(false);
            UpdateUnsavedState();
        }

        private void RefreshInspector(bool force)
        {
            if (inspector == null)
            {
                return;
            }

            var graph = HasOpenQuest ? session.Graph : null;
            inspector.Show(serializedGraph, graph, graphView?.SelectedNodeId, force);
        }

        private void OnInspectorNodeChanged(string nodeId)
        {
            if (!HasOpenQuest || graphView == null)
            {
                return;
            }

            if (graphView.HasPortsMatchingModel(nodeId))
            {
                graphView.RefreshNode(nodeId);
            }
            else
            {
                var removed = QuestGraphEditing.RemoveDanglingEdges(session.Graph, nodeId);
                serializedGraph?.Update();
                graphView.Rebuild();
                graphView.SelectNode(nodeId);
                if (removed > 0)
                {
                    ShowNotification(new GUIContent($"Удалено связей к исчезнувшим портам: {removed}."));
                }
            }

            UpdateUnsavedState();
        }

        private void OnViewTransformChanged(GraphView view)
        {
            hasStoredView = true;
            viewPosition = view.viewTransform.position;
            viewScale = view.viewTransform.scale;
        }

        private void RestoreOrFrameView()
        {
            if (graphView == null || !HasOpenQuest)
            {
                return;
            }

            if (hasStoredView)
            {
                graphView.UpdateViewTransform(viewPosition, viewScale);
            }
            else
            {
                graphView.schedule.Execute(() => graphView.FrameAll());
            }
        }

        private void OnQuestAssetsChanged()
        {
            library?.Refresh();

            if (HasOpenQuest && !session.IsDraft && session.LoadAsset() == null)
            {
                session.DetachFromAsset();
                library?.SetCurrent(null);
                ShowNotification(new GUIContent("Файл задания удалён. Сохраните задание заново (Save As)."));
            }

            UpdateUnsavedState();
        }

        private bool ConfirmCloseCurrent()
        {
            if (!HasOpenQuest || !session.RefreshUnsavedState())
            {
                return true;
            }

            var choice = EditorUtility.DisplayDialogComplex(
                "Несохранённые изменения",
                $"Сохранить изменения в задании «{session.Title}»?",
                "Сохранить",
                "Отмена",
                "Не сохранять");

            switch (choice)
            {
                case 0:
                    return SaveCurrent();
                case 2:
                    return true;
                default:
                    return false;
            }
        }

        private void NewQuest()
        {
            if (!ConfirmCloseCurrent())
            {
                return;
            }

            var questId = QuestAssetUtility.MakeUniqueQuestId(NewQuestId, QuestAssetUtility.CollectQuestIds(null));
            ReplaceSession(QuestWorkingCopy.CreateDraft(questId));
            library?.Select(null);
        }

        private void OpenQuestFromFile()
        {
            var absolutePath = EditorUtility.OpenFilePanel("Открыть задание", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(absolutePath))
            {
                return;
            }

            var path = FileUtil.GetProjectRelativePath(absolutePath.Replace('\\', '/'));
            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Не удалось открыть", "Выбранный файл не является заданием (QuestGraphAsset) внутри проекта.", "OK");
                return;
            }

            OpenAsset(asset);
        }

        private bool SaveCurrent()
        {
            if (!HasOpenQuest)
            {
                return false;
            }

            if (session.IsDraft || session.LoadAsset() == null)
            {
                return SaveCurrentAs();
            }

            if (QuestEditorValidation.HasMissingTypes(session.LoadAsset())
                && !EditorUtility.DisplayDialog(
                    "Узлы с отсутствующими классами",
                    "В ассете есть узлы, классы которых не найдены. При сохранении их данные будут потеряны. Сначала верните классы или добавьте [MovedFrom].\n\nВсё равно сохранить?",
                    "Сохранить",
                    "Отмена"))
            {
                return false;
            }

            if (!session.Save(out var error))
            {
                EditorUtility.DisplayDialog("Не удалось сохранить", error, "OK");
                return false;
            }

            OnSaved();
            return true;
        }

        private bool SaveCurrentAs()
        {
            if (!HasOpenQuest)
            {
                return false;
            }

            var path = EditorUtility.SaveFilePanelInProject("Сохранить задание", session.Title, "asset", "Выберите, куда сохранить задание.");
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            EnsureUniqueQuestId(path);

            if (!session.SaveAs(path, out var error))
            {
                EditorUtility.DisplayDialog("Не удалось сохранить", error, "OK");
                return false;
            }

            OnSaved();
            return true;
        }

        private void EnsureUniqueQuestId(string targetPath)
        {
            var graph = session.Graph;
            var uniqueId = QuestAssetUtility.MakeUniqueQuestId(graph.QuestId, QuestAssetUtility.CollectQuestIds(targetPath));
            if (uniqueId == graph.QuestId)
            {
                return;
            }

            Undo.RecordObject(graph, "Change Quest Id");
            graph.QuestId = uniqueId;
            serializedGraph?.Update();
            ShowNotification(new GUIContent($"questId изменён на «{uniqueId}»: прежний уже занят."));
        }

        private void OnSaved()
        {
            hasUnsavedChanges = false;
            library?.Refresh();
            library?.SetCurrent(session.AssetGuid);
            library?.Select(session.LoadAsset());
            var diagnostics = RunValidation();
            UpdateUnsavedState();

            var errorCount = diagnostics?.Count(d => d.Severity == QuestDiagnosticSeverity.Error) ?? 0;
            if (errorCount > 0)
            {
                ShowNotification(new GUIContent($"Сохранено с ошибками ({errorCount}). Задание не будет запущено в игре."));
            }
        }

        private void CreateQuestAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Создать задание", "NewQuest", "asset", "Выберите, где создать ассет задания.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                EditorUtility.DisplayDialog("Файл уже существует", "Выберите другое имя: создание не перезаписывает существующие ассеты.", "OK");
                return;
            }

            if (!ConfirmCloseCurrent())
            {
                return;
            }

            var asset = QuestAssetUtility.CreateQuestAsset(path);
            library?.Refresh();
            ReplaceSession(QuestWorkingCopy.Open(asset));
            library?.Select(asset);
        }

        private void DuplicateQuestAsset(QuestGraphAsset source)
        {
            var copy = QuestAssetUtility.DuplicateQuestAsset(source);
            if (copy == null)
            {
                EditorUtility.DisplayDialog("Не удалось дублировать", $"Не удалось создать копию «{source.name}».", "OK");
                return;
            }

            library?.Refresh();
            library?.Select(copy);
        }

        private void DeleteQuestAsset(QuestGraphAsset asset)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            var isOpen = HasOpenQuest && session.AssetGuid == guid;
            var message = $"Удалить задание «{asset.name}»? Файл будет перемещён в корзину.";
            if (isOpen && session.RefreshUnsavedState())
            {
                message += "\n\nЗадание открыто, несохранённые изменения будут потеряны.";
            }

            if (!EditorUtility.DisplayDialog("Удалить задание", message, "Удалить", "Отмена"))
            {
                return;
            }

            if (isOpen)
            {
                ReplaceSession(null);
            }

            if (!QuestAssetUtility.DeleteQuestAsset(asset))
            {
                EditorUtility.DisplayDialog("Не удалось удалить", $"Не удалось удалить «{asset.name}».", "OK");
            }

            library?.Refresh();
        }
    }
}
