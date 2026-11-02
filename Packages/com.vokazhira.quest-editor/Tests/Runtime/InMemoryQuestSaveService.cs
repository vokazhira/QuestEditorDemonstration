using System;
using UnityEngine;

namespace Vokazhira.QuestEditor.Tests
{
    /// <summary>
    /// Хранит сохранение в строке JSON (через JsonUtility, как файловый сервис) и умеет имитировать сбои.
    /// </summary>
    internal sealed class InMemoryQuestSaveService : IQuestSaveService
    {
        public string Json;
        public string LoadError;
        public bool ThrowOnSave;

        public bool TryLoad(out QuestSaveData data, out string error)
        {
            if (LoadError != null)
            {
                data = null;
                error = LoadError;
                return false;
            }

            data = string.IsNullOrEmpty(Json) ? new QuestSaveData() : JsonUtility.FromJson<QuestSaveData>(Json);
            data.Normalize();
            error = null;
            return true;
        }

        public void Save(QuestSaveData data)
        {
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("disk full");
            }

            Json = JsonUtility.ToJson(data);
        }
    }
}
