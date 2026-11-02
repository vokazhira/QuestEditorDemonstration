using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Жизненный цикл заданий и маршрутизация событий игры. Обычный C#-класс: создаётся игрой, не синглтон.
    /// Исполнение графа — <see cref="QuestGraphRunner"/>, выдача наград — <see cref="QuestRewardDispatcher"/>.
    /// Задания с ошибками валидации регистрируются, но не запускаются.
    /// </summary>
    public sealed class QuestManager : IQuestConditionContext, IDisposable
    {
        private const string LogTag = "QuestManager";

        private readonly Dictionary<string, QuestInstance> instancesById = new Dictionary<string, QuestInstance>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<QuestDiagnostic>> errorsById = new Dictionary<string, List<QuestDiagnostic>>(StringComparer.Ordinal);
        private readonly List<QuestInstance> instances = new List<QuestInstance>();
        private readonly QuestGraphRunner runner;
        private readonly QuestRewardDispatcher rewardDispatcher = new QuestRewardDispatcher();
        private readonly List<QuestInstanceSaveData> orphanedRecords = new List<QuestInstanceSaveData>();
        private readonly Dictionary<string, QuestInstanceSaveData> frozenRecords = new Dictionary<string, QuestInstanceSaveData>(StringComparer.Ordinal);
        private readonly IQuestSaveService saveService;
        private readonly ILogger logger;
        private bool disposed;

        /// <param name="database">Задания игры.</param>
        /// <param name="saveService">Хранилище прогресса для <see cref="Save"/> / <see cref="Load"/>; может быть null,
        /// если игра сохраняет данные <see cref="Capture"/> сама.</param>
        /// <param name="logger">Куда писать сообщения менеджера; по умолчанию <c>Debug.unityLogger</c>.</param>
        public QuestManager(QuestDatabase database, IQuestSaveService saveService, ILogger logger = null)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            this.saveService = saveService;
            this.logger = logger ?? Debug.unityLogger;
            runner = new QuestGraphRunner(this);
            RegisterQuests(database.Quests);
        }

        public event Action<QuestInstance> QuestStarted;

        /// <summary>
        /// Прогресс цели изменился: задание, цель и новое значение прогресса.
        /// </summary>
        public event Action<QuestInstance, ObjectiveNodeData, int> QuestProgressChanged;

        /// <summary>
        /// Задание дошло до End(Completed), но не все награды выданы (ManualClaim или ошибка выдачи).
        /// </summary>
        public event Action<QuestInstance> QuestClaimable;

        public event Action<QuestInstance> QuestCompleted;

        public event Action<QuestInstance> QuestFailed;

        public event Action<QuestInstance, RewardNodeData> QuestRewardGranted;

        /// <summary>
        /// Награда не выдана: задание, награда и причина.
        /// </summary>
        public event Action<QuestInstance, RewardNodeData, string> QuestRewardFailed;

        public QuestState GetState(string questId)
        {
            return questId != null && instancesById.TryGetValue(questId, out var instance) ? instance.State : QuestState.Available;
        }

        public QuestInstance GetInstance(string questId)
        {
            return questId != null && instancesById.TryGetValue(questId, out var instance) ? instance : null;
        }

        public IReadOnlyList<QuestInstance> GetInstances()
        {
            return instances;
        }

        /// <summary>
        /// Ошибки валидации задания; пустой список, если задание валидно или не зарегистрировано.
        /// </summary>
        public IReadOnlyList<QuestDiagnostic> GetValidationErrors(string questId)
        {
            return questId != null && errorsById.TryGetValue(questId, out var errors) ? errors : (IReadOnlyList<QuestDiagnostic>)Array.Empty<QuestDiagnostic>();
        }

        public bool CanStart(string questId)
        {
            return CanStart(questId, out _);
        }

        public bool Start(string questId)
        {
            ThrowIfDisposed();
            if (!CanStart(questId, out var reason))
            {
                logger.LogWarning(LogTag, $"Задание «{questId}» нельзя запустить: {reason}");
                return false;
            }

            var instance = instancesById[questId];
            WarnAboutMissingRewardHandlers(instance);

            instance.ResetProgress();
            instance.State = QuestState.Active;
            QuestStarted?.Invoke(instance);

            var outcome = runner.Begin(instance, out var error);
            HandleOutcome(instance, outcome, error);
            return true;
        }

        /// <summary>
        /// Прерывает активное задание: оно становится Failed.
        /// </summary>
        public bool Abandon(string questId)
        {
            ThrowIfDisposed();
            var instance = GetInstance(questId);
            if (instance == null || instance.State != QuestState.Active || instance.IsFrozen)
            {
                return false;
            }

            instance.State = QuestState.Failed;
            QuestFailed?.Invoke(instance);
            return true;
        }

        /// <summary>
        /// Сообщает о событии игры всем активным заданиям. События для завершённых заданий игнорируются.
        /// </summary>
        public void Report(in QuestEvent questEvent)
        {
            ThrowIfDisposed();
            var snapshot = instances.ToArray();
            foreach (var instance in snapshot)
            {
                if (instance.State != QuestState.Active || instance.IsFrozen)
                {
                    continue;
                }

                try
                {
                    ApplyEvent(instance, questEvent);
                }
                catch (Exception exception)
                {
                    logger.LogException(exception);
                }
            }
        }

        /// <summary>
        /// Выдаёт невыданные награды задания в состоянии Claimable. Возвращает true, если задание стало Completed.
        /// </summary>
        public bool Claim(string questId)
        {
            ThrowIfDisposed();
            var instance = GetInstance(questId);
            if (instance == null || instance.State != QuestState.Claimable || instance.IsFrozen)
            {
                return false;
            }

            return GrantPendingRewards(instance, false);
        }

        /// <summary>
        /// Снимок прогресса всех заданий для сохранения. Замороженные и «осиротевшие» записи возвращаются без изменений.
        /// </summary>
        public QuestSaveData Capture()
        {
            ThrowIfDisposed();
            var data = new QuestSaveData();
            foreach (var instance in instances)
            {
                if (frozenRecords.TryGetValue(instance.QuestId, out var frozen))
                {
                    data.quests.Add(frozen.Clone());
                }
                else if (instance.State != QuestState.Available)
                {
                    data.quests.Add(QuestSaveMapper.ToRecord(instance));
                }
            }

            foreach (var orphan in orphanedRecords)
            {
                data.orphanedQuests.Add(orphan.Clone());
            }

            return data;
        }

        /// <summary>
        /// Восстанавливает прогресс. Задания, которых нет в сохранении, становятся Available. Записи заданий, которых нет
        /// в базе, сохраняются как «осиротевшие»; задания, которые нельзя продолжить, замораживаются (<see cref="QuestInstance.IsFrozen"/>).
        /// Возвращает false, если сохранение сделано более новой версией формата; текущее состояние тогда не меняется.
        /// </summary>
        public bool Restore(QuestSaveData data)
        {
            ThrowIfDisposed();
            if (data != null && data.saveVersion > QuestSaveData.CurrentVersion)
            {
                logger.LogError(LogTag, $"Версия сохранения {data.saveVersion} новее поддерживаемой ({QuestSaveData.CurrentVersion}). Прогресс не загружен.");
                return false;
            }

            if (data != null && data.saveVersion < QuestSaveData.CurrentVersion)
            {
                logger.LogWarning(LogTag, $"Версия сохранения {data.saveVersion} старше текущей ({QuestSaveData.CurrentVersion}); миграций нет, данные читаются как есть.");
            }

            foreach (var instance in instances)
            {
                instance.ResetToAvailable();
            }

            orphanedRecords.Clear();
            frozenRecords.Clear();
            if (data == null)
            {
                return true;
            }

            var restoredIds = new HashSet<string>(StringComparer.Ordinal);
            RestoreRecords(data.quests, restoredIds);
            RestoreRecords(data.orphanedQuests, restoredIds);
            AdvanceSatisfiedObjectives();
            return true;
        }

        /// <summary>
        /// Сохраняет <see cref="Capture"/> через <see cref="IQuestSaveService"/>. Ошибки пишутся в лог.
        /// </summary>
        public bool Save()
        {
            ThrowIfDisposed();
            if (saveService == null)
            {
                logger.LogError(LogTag, "Сохранение невозможно: менеджер создан без IQuestSaveService.");
                return false;
            }

            try
            {
                saveService.Save(Capture());
                return true;
            }
            catch (Exception exception)
            {
                logger.LogException(exception);
                return false;
            }
        }

        /// <summary>
        /// Загружает прогресс через <see cref="IQuestSaveService"/> и вызывает <see cref="Restore"/>.
        /// При ошибке загрузки состояние не меняется, причина пишется в лог.
        /// </summary>
        public bool Load()
        {
            ThrowIfDisposed();
            if (saveService == null)
            {
                logger.LogError(LogTag, "Загрузка невозможна: менеджер создан без IQuestSaveService.");
                return false;
            }

            QuestSaveData data;
            string error;
            try
            {
                if (!saveService.TryLoad(out data, out error))
                {
                    logger.LogError(LogTag, $"Прогресс заданий не загружен: {error}");
                    return false;
                }
            }
            catch (Exception exception)
            {
                logger.LogException(exception);
                return false;
            }

            return Restore(data);
        }

        public void RegisterRewardHandler<TReward>(IRewardHandler<TReward> handler) where TReward : RewardNodeData
        {
            ThrowIfDisposed();
            rewardDispatcher.Register(handler);
        }

        /// <summary>
        /// Отписывает всех подписчиков событий и удаляет обработчики наград. После вызова менеджер использовать нельзя.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            QuestStarted = null;
            QuestProgressChanged = null;
            QuestClaimable = null;
            QuestCompleted = null;
            QuestFailed = null;
            QuestRewardGranted = null;
            QuestRewardFailed = null;
            rewardDispatcher.Clear();
        }

        private void RegisterQuests(IReadOnlyList<QuestGraphAsset> quests)
        {
            foreach (var graph in quests)
            {
                if (graph == null)
                {
                    logger.LogWarning(LogTag, $"В базе заданий есть пустая ссылка — пропущена.");
                    continue;
                }

                var questId = graph.QuestId;
                if (!string.IsNullOrEmpty(questId) && instancesById.ContainsKey(questId))
                {
                    if (!ReferenceEquals(instancesById[questId].Graph, graph))
                    {
                        logger.LogError(LogTag, $"Задание «{graph.name}» не зарегистрировано: questId «{questId}» уже занят.");
                    }

                    continue;
                }

                var errors = QuestGraphValidator.Validate(graph, quests).FindAll(d => d.Severity == QuestDiagnosticSeverity.Error);
                if (string.IsNullOrEmpty(questId))
                {
                    logger.LogError(LogTag, $"Задание «{graph.name}» не зарегистрировано: пустой questId.");
                    continue;
                }

                var instance = new QuestInstance(graph);
                instances.Add(instance);
                instancesById.Add(questId, instance);

                if (errors.Count > 0)
                {
                    errorsById.Add(questId, errors);
                    logger.LogError(LogTag, $"Задание «{questId}» содержит ошибки и не будет запускаться:\n{string.Join("\n", errors)}");
                }
            }
        }

        private void RestoreRecords(List<QuestInstanceSaveData> records, HashSet<string> restoredIds)
        {
            if (records == null)
            {
                return;
            }

            foreach (var record in records)
            {
                if (record == null || string.IsNullOrEmpty(record.questId))
                {
                    logger.LogWarning(LogTag, "В сохранении есть запись без questId — пропущена.");
                    continue;
                }

                if (!restoredIds.Add(record.questId))
                {
                    logger.LogWarning(LogTag, $"В сохранении несколько записей задания «{record.questId}» — используется первая.");
                    continue;
                }

                if (!instancesById.TryGetValue(record.questId, out var instance))
                {
                    orphanedRecords.Add(record.Clone());
                    logger.LogWarning(LogTag, $"Задания «{record.questId}» нет в базе. Его прогресс сохранён и будет восстановлен, когда задание вернётся.");
                    continue;
                }

                var applied = QuestSaveMapper.TryApply(instance, record, out var problem);
                var needsGraph = instance.State == QuestState.Active || instance.State == QuestState.Claimable;
                if (applied && needsGraph && errorsById.ContainsKey(record.questId))
                {
                    applied = false;
                    problem = "граф задания содержит ошибки валидации";
                }

                if (!applied)
                {
                    instance.IsFrozen = true;
                    frozenRecords[record.questId] = record.Clone();
                    logger.LogWarning(LogTag, $"Задание «{record.questId}» заморожено: {problem}. Оно не будет исполняться, запись сохранится без изменений.");
                }
            }
        }

        /// <summary>
        /// После загрузки продвигает задания, у которых текущая цель уже выполнена (например, требуемое количество уменьшили).
        /// </summary>
        private void AdvanceSatisfiedObjectives()
        {
            foreach (var instance in instances.ToArray())
            {
                var objective = instance.CurrentObjective;
                if (objective != null && instance.GetProgress(objective.ObjectiveId) >= objective.RequiredCount)
                {
                    var outcome = runner.Advance(instance, out var error);
                    HandleOutcome(instance, outcome, error);
                }
            }
        }

        private bool CanStart(string questId, out string reason)
        {
            if (disposed)
            {
                reason = "менеджер уничтожен (Dispose).";
                return false;
            }

            var instance = GetInstance(questId);
            if (instance == null)
            {
                reason = "задание не зарегистрировано в базе.";
                return false;
            }

            if (errorsById.ContainsKey(questId))
            {
                reason = "граф задания содержит ошибки валидации.";
                return false;
            }

            if (instance.IsFrozen)
            {
                reason = "задание заморожено после загрузки сохранения.";
                return false;
            }

            if (instance.State != QuestState.Available)
            {
                reason = $"задание уже в состоянии {instance.State}.";
                return false;
            }

            reason = null;
            return true;
        }

        private void ApplyEvent(QuestInstance instance, in QuestEvent questEvent)
        {
            if (!runner.TryApplyEvent(instance, questEvent, out var objective, out var progress))
            {
                return;
            }

            QuestProgressChanged?.Invoke(instance, objective, progress);
            if (instance.State == QuestState.Active && progress >= objective.RequiredCount)
            {
                var outcome = runner.Advance(instance, out var error);
                HandleOutcome(instance, outcome, error);
            }
        }

        private void HandleOutcome(QuestInstance instance, QuestAdvanceOutcome outcome, string error)
        {
            switch (outcome)
            {
                case QuestAdvanceOutcome.WaitingForObjective:
                    return;

                case QuestAdvanceOutcome.ReachedFailedEnd:
                    instance.State = QuestState.Failed;
                    QuestFailed?.Invoke(instance);
                    return;

                case QuestAdvanceOutcome.ReachedCompletedEnd:
                    instance.State = QuestState.Claimable;
                    if (instance.Graph.RewardMode == QuestRewardMode.AutoGrant)
                    {
                        GrantPendingRewards(instance, true);
                    }
                    else
                    {
                        QuestClaimable?.Invoke(instance);
                    }

                    return;

                default:
                    logger.LogError(LogTag, $"Задание «{instance.QuestId}» остановлено с ошибкой: {error}");
                    instance.State = QuestState.Failed;
                    QuestFailed?.Invoke(instance);
                    return;
            }
        }

        /// <summary>
        /// Выдаёт награды, подключённые к текущему End(Completed), кроме уже выданных.
        /// Если все выданы — задание становится Completed, иначе остаётся Claimable.
        /// </summary>
        private bool GrantPendingRewards(QuestInstance instance, bool notifyClaimableOnFailure)
        {
            var allGranted = true;
            foreach (var reward in CollectRewards(instance))
            {
                if (instance.IsRewardGranted(reward.RewardId))
                {
                    continue;
                }

                if (rewardDispatcher.TryGrant(reward, instance.QuestId, out var error))
                {
                    instance.MarkRewardGranted(reward.RewardId);
                    QuestRewardGranted?.Invoke(instance, reward);
                }
                else
                {
                    allGranted = false;
                    logger.LogWarning(LogTag, $"Задание «{instance.QuestId}»: награда «{reward.GetSummary()}» не выдана. {error}");
                    QuestRewardFailed?.Invoke(instance, reward, error);
                }
            }

            if (!allGranted)
            {
                if (notifyClaimableOnFailure)
                {
                    QuestClaimable?.Invoke(instance);
                }

                return false;
            }

            instance.State = QuestState.Completed;
            QuestCompleted?.Invoke(instance);
            return true;
        }

        private static List<RewardNodeData> CollectRewards(QuestInstance instance)
        {
            var rewards = new List<RewardNodeData>();
            var endId = instance.CurrentNodeId;
            foreach (var edge in instance.Graph.Edges)
            {
                if (edge == null
                    || !string.Equals(edge.ToNodeId, endId, StringComparison.Ordinal)
                    || !string.Equals(edge.ToPortId, QuestPortIds.Rewards, StringComparison.Ordinal))
                {
                    continue;
                }

                if (instance.Graph.FindNode(edge.FromNodeId) is RewardNodeData reward)
                {
                    rewards.Add(reward);
                }
            }

            return rewards;
        }

        private void WarnAboutMissingRewardHandlers(QuestInstance instance)
        {
            foreach (var node in instance.Graph.Nodes)
            {
                if (node is RewardNodeData reward && !rewardDispatcher.HasHandler(reward.GetType()))
                {
                    logger.LogWarning(LogTag, $"Задание «{instance.QuestId}»: нет обработчика для награды {reward.GetType().Name}. "
                        + "Она не будет выдана, пока обработчик не зарегистрирован (задание останется Claimable).");
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(QuestManager));
            }
        }
    }
}
