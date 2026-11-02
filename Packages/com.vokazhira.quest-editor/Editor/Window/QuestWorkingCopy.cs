using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vokazhira.QuestEditor.Editor
{
    /// <summary>
    /// Рабочая копия задания, которую правит окно. Сериализуется вместе с окном и переживает domain reload.
    /// Ассет меняется только при <see cref="Save"/> / <see cref="SaveAs"/>.
    /// </summary>
    [Serializable]
    internal sealed class QuestWorkingCopy
    {
        public const string DraftName = "New Quest";

        [SerializeField] private string assetGuid;
        [SerializeField] private QuestGraphAsset graph;
        [SerializeField] private string savedSnapshot;
        [SerializeField] private bool hasUnsavedChanges;
        [NonSerialized] private string currentSnapshot;

        public QuestGraphAsset Graph => graph;
        public string AssetGuid => assetGuid;
        public bool IsDraft => string.IsNullOrEmpty(assetGuid);
        public bool IsAlive => graph != null;
        public bool HasUnsavedChanges => hasUnsavedChanges;

        /// <summary>
        /// JSON-снимок рабочей копии на момент последнего <see cref="RefreshUnsavedState"/> или сохранения.
        /// </summary>
        public string CurrentSnapshot => graph == null ? null : currentSnapshot ?? (currentSnapshot = TakeSnapshot(graph));

        public string Title
        {
            get
            {
                var asset = LoadAsset();
                if (asset != null)
                {
                    return asset.name;
                }

                return graph != null ? graph.name : string.Empty;
            }
        }

        public static QuestWorkingCopy Open(QuestGraphAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Quest graph must be a saved asset.", nameof(asset));
            }

            var copy = Object.Instantiate(asset);
            copy.name = asset.name;
            copy.hideFlags = HideFlags.DontSave;

            var workingCopy = new QuestWorkingCopy
            {
                assetGuid = AssetDatabase.AssetPathToGUID(path),
                graph = copy
            };
            workingCopy.savedSnapshot = TakeSnapshot(copy);
            workingCopy.hasUnsavedChanges = false;
            return workingCopy;
        }

        public static QuestWorkingCopy CreateDraft(string questId)
        {
            var draft = ScriptableObject.CreateInstance<QuestGraphAsset>();
            draft.name = DraftName;
            draft.hideFlags = HideFlags.DontSave;
            draft.QuestId = questId;
            draft.DisplayName = DraftName;
            QuestAssetUtility.PopulateDefaultGraph(draft);

            return new QuestWorkingCopy
            {
                assetGuid = null,
                graph = draft,
                savedSnapshot = null,
                hasUnsavedChanges = true
            };
        }

        public QuestGraphAsset LoadAsset()
        {
            if (IsDraft)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(path);
        }

        /// <summary>
        /// Сравнивает рабочую копию с последним сохранённым состоянием. Черновик всегда считается несохранённым.
        /// </summary>
        public bool RefreshUnsavedState()
        {
            currentSnapshot = graph != null ? TakeSnapshot(graph) : null;
            hasUnsavedChanges = graph != null && (IsDraft || !string.Equals(savedSnapshot, currentSnapshot, StringComparison.Ordinal));
            return hasUnsavedChanges;
        }

        public bool Save(out string error)
        {
            var asset = LoadAsset();
            if (asset == null)
            {
                error = "The quest asset was not found. Use Save As.";
                return false;
            }

            CopyInto(asset);
            error = null;
            return true;
        }

        public bool SaveAs(string path, out string error)
        {
            if (graph == null)
            {
                error = "No quest is open.";
                return false;
            }

            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                error = "The path must point to an .asset file inside the project.";
                return false;
            }

            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            QuestGraphAsset target;
            if (existing == null)
            {
                target = ScriptableObject.CreateInstance<QuestGraphAsset>();
                target.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(target, path);
            }
            else if (existing is QuestGraphAsset existingGraph)
            {
                target = existingGraph;
            }
            else
            {
                error = "An asset of another type already exists at this path.";
                return false;
            }

            CopyInto(target);
            assetGuid = AssetDatabase.AssetPathToGUID(path);
            error = null;
            return true;
        }

        /// <summary>
        /// Отвязывает копию от ассета (например, ассет удалён). Копия становится черновиком.
        /// </summary>
        public void DetachFromAsset()
        {
            assetGuid = null;
            savedSnapshot = null;
            RefreshUnsavedState();
        }

        public void Dispose()
        {
            if (graph != null)
            {
                Undo.ClearUndo(graph);
                Object.DestroyImmediate(graph);
            }

            graph = null;
            hasUnsavedChanges = false;
        }

        private void CopyInto(QuestGraphAsset asset)
        {
            var assetName = asset.name;
            EditorUtility.CopySerialized(graph, asset);
            asset.name = assetName;
            asset.hideFlags = HideFlags.None;
            asset.InvalidateNodeIndex();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);

            savedSnapshot = TakeSnapshot(graph);
            currentSnapshot = savedSnapshot;
            hasUnsavedChanges = false;
        }

        private static string TakeSnapshot(QuestGraphAsset source)
        {
            return EditorJsonUtility.ToJson(source);
        }
    }
}
