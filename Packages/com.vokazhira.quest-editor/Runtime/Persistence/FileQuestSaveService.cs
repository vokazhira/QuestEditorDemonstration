using System;
using System.IO;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Сохраняет прогресс в JSON-файл (по умолчанию <c>Application.persistentDataPath/quests.json</c>).
    /// Запись атомарная: сначала временный файл, затем замена. Повреждённый файл копируется в <c>.corrupt</c>.
    /// </summary>
    public sealed class FileQuestSaveService : IQuestSaveService
    {
        public const string DefaultFileName = "quests.json";
        public const string TempSuffix = ".tmp";
        public const string CorruptSuffix = ".corrupt";

        public FileQuestSaveService()
            : this(DefaultFileName)
        {
        }

        /// <param name="filePath">Абсолютный путь или путь относительно <c>Application.persistentDataPath</c>.</param>
        public FileQuestSaveService(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be empty.", nameof(filePath));
            }

            FilePath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(Application.persistentDataPath, filePath);
        }

        public string FilePath { get; }

        public bool TryLoad(out QuestSaveData data, out string error)
        {
            if (!File.Exists(FilePath))
            {
                data = new QuestSaveData();
                error = null;
                return true;
            }

            string json;
            try
            {
                json = File.ReadAllText(FilePath);
            }
            catch (Exception exception)
            {
                data = null;
                error = $"Не удалось прочитать «{FilePath}»: {exception.Message}";
                return false;
            }

            QuestSaveData parsed = null;
            string parseError = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                parseError = "файл пуст";
            }
            else
            {
                try
                {
                    parsed = JsonUtility.FromJson<QuestSaveData>(json);
                    if (parsed == null)
                    {
                        parseError = "JSON не содержит данных сохранения";
                    }
                }
                catch (Exception exception)
                {
                    parseError = exception.Message;
                }
            }

            if (parsed == null)
            {
                data = null;
                error = $"Сохранение «{FilePath}» повреждено ({parseError}). {BackupCorruptFile()}";
                return false;
            }

            parsed.Normalize();
            data = parsed;
            error = null;
            return true;
        }

        public void Save(QuestSaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = FilePath + TempSuffix;
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            ReplaceWithTemp(tempPath);
        }

        private void ReplaceWithTemp(string tempPath)
        {
            if (!File.Exists(FilePath))
            {
                File.Move(tempPath, FilePath);
                return;
            }

            try
            {
                File.Replace(tempPath, FilePath, null);
            }
            catch (Exception exception) when (exception is PlatformNotSupportedException || exception is IOException)
            {
                File.Copy(tempPath, FilePath, true);
                File.Delete(tempPath);
            }
        }

        private string BackupCorruptFile()
        {
            var corruptPath = FilePath + CorruptSuffix;
            try
            {
                File.Copy(FilePath, corruptPath, true);
                return $"Копия сохранена в «{corruptPath}».";
            }
            catch (Exception exception)
            {
                return $"Не удалось сохранить копию: {exception.Message}";
            }
        }
    }
}
