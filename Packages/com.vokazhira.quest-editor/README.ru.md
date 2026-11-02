# Quest Editor

[English version](README.md)

Визуальный редактор заданий на графе для Unity и runtime-система их исполнения.

Задание собирается из узлов в окне **Tools → Quest Editor**: узлы соединяются, настраиваются и сохраняются в ассет.
Этот же ассет исполняется в игре без Editor API.

```
[Start] → [Kill: goblin ×10] → [Condition: задание "intro" выполнено?] ─True──→ [End: Completed] ← [Reward: 100 gold]
                                                                         │                         ← [Reward: 25 XP]
                                                                         └False─→ [End: Failed]
```

- **Unity:** 2023.1.22f1 (в пакете указано `"unity": "2023.1"`).
- **Зависимости:** только встроенные модули (`jsonserialize`, `uielements`). Сторонних пакетов нет.
- **Расширяемость:** новые типы целей, условий и наград добавляются в вашем проекте без изменения пакета.

Интерфейс редактора, названия узлов и сообщения пакета — на английском; ниже они приведены так, как их видно в Unity.

## Содержание

- [Установка](#установка)
- [Структура пакета](#структура-пакета)
- [Первое задание](#первое-задание)
- [Узлы и связи](#узлы-и-связи)
- [Валидация](#валидация)
- [Исполнение: QuestManager](#исполнение-questmanager)
- [Сохранение прогресса](#сохранение-прогресса)
- [Свои цели, условия и награды](#свои-цели-условия-и-награды)
- [Переименование классов узлов: `[MovedFrom]`](#переименование-классов-узлов-movedfrom)
- [Пример](#пример)
- [Запуск тестов](#запуск-тестов)
- [Ограничения](#ограничения)
- [План развития](#план-развития)

## Установка

**По Git URL.** В Unity: **Window → Package Manager → + → Add package from git URL…**, затем адрес:

```
https://github.com/vokazhira/QuestEditorDemonstration.git?path=/Packages/com.vokazhira.quest-editor
```

Чтобы закрепить ветку или тег, добавьте в конец `#<ветка-или-тег>`.

**Локально.** Скопируйте папку `com.vokazhira.quest-editor` в `Packages/` вашего проекта (embedded-пакет)
или выберите **+ → Add package from disk…** и укажите её `package.json`.

## Структура пакета

```
com.vokazhira.quest-editor/
├── package.json, README.md, README.ru.md, CHANGELOG.md, LICENSE.md
├── Runtime/        Vokazhira.QuestEditor          (без ссылок на UnityEditor)
│   ├── Graph/          QuestGraphAsset, QuestNodeData, QuestEdgeData, порты, QuestPortRules, [QuestNode]
│   ├── Nodes/          Start, End, Objectives/, Conditions/, Rewards/
│   ├── Validation/     QuestGraphValidator, QuestDiagnostic, коды диагностик
│   ├── Execution/      QuestManager, QuestInstance, QuestGraphRunner, QuestRewardDispatcher, QuestEvent, QuestDatabase
│   ├── Services/       ICurrencyService, IExperienceService, IRewardHandler<T>, встроенные обработчики наград
│   └── Persistence/    DTO сохранений, IQuestSaveService, FileQuestSaveService
├── Editor/         Vokazhira.QuestEditor.Editor   (окно, граф, инспектор, библиотека)
├── Tests/          EditMode-тесты (runtime-логика и редактор)
└── Samples~/BasicQuestExample/
```

Все runtime-типы — в одном namespace `Vokazhira.QuestEditor`, редакторские — в `Vokazhira.QuestEditor.Editor`.

## Первое задание

1. Откройте **Tools → Quest Editor**.
2. Нажмите **New** — появится черновик с узлами **Start** и **End (Completed)**.
3. Нажмите **Settings** и задайте уникальный **Quest Id** (например, `forest_cleanup`), название и режим наград.
4. Правый клик по холсту → **Create Node** (или **Space**) → **Objectives/Kill Enemies**. Узел появится под курсором.
5. Выделите узел и в **Inspector** справа укажите **Enemy Id** = `goblin`, **Required Count** = `10`.
6. Протяните связи: **Start.Next → Kill.In**, **Kill.Done → End.In**.
7. Добавьте **Rewards/Gold** и соедините **Gold.Reward → End.Rewards**.
8. Нажмите **Validate**: внизу появятся ошибки и предупреждения; клик по строке переходит к узлу.
9. Нажмите **Save** и выберите, где сохранить ассет.
10. Создайте базу заданий (**Create → Quest Editor → Quest Database**) и нажмите в её инспекторе **Collect All Project Quests**.

Двойной клик по ассету задания в окне Project тоже открывает его в редакторе. Все изменения проходят через Undo
(**Ctrl+Z / Ctrl+Y**). Окно правит рабочую копию: ассет меняется только при **Save**, а при закрытии окна или открытии
другого задания с несохранёнными изменениями появляется диалог **Save / Don't Save / Cancel**.

## Узлы и связи

Порты бывают двух видов: **Flow** (порядок исполнения) и **Reward** (подключение наград к финалу).

| Узел | Входы | Выходы | Назначение |
|---|---|---|---|
| **Start** | — | Flow `Next` (single) | Начало. Ровно один на граф. |
| **Objective** | Flow `In` (multi) | Flow `Done` (single) | Ждёт событий игры, пока прогресс меньше требуемого. |
| **Condition** | Flow `In` (multi) | Flow `True`, `False` (single) | Мгновенная проверка и ветвление. |
| **Reward** | — | Reward `Reward` (single) | Описание награды; в потоке не участвует. |
| **End** | Flow `In` (multi), Reward `Rewards` (multi, только у Completed) | — | Финал с результатом **Completed** или **Failed**. |

Правила соединений (`QuestPortRules`, общие для редактора и валидатора): только выход → вход; только порты одного вида;
нельзя соединять узел сам с собой; соблюдается capacity порта; Flow-циклы запрещены. Несовместимые порты не подсвечиваются
при перетаскивании связи; связь, замыкающая цикл, отклоняется с сообщением.

**Встроенные типы**

| Тип | Поля | Реагирует на |
|---|---|---|
| `KillObjective` | `enemyId`, `requiredCount` | `QuestEvent.EnemyKilled(enemyId)` |
| `CollectObjective` | `itemId`, `requiredCount` | `QuestEvent.ItemCollected(itemId)` |
| `CompleteWaveObjective` | `waveId` (пусто — любая волна), `requiredCount` | `QuestEvent.WaveCompleted(waveId)` |
| `QuestCompletedCondition` | `questId` | `True`, если это задание `Completed` в момент проверки |
| `GoldReward`, `ExperienceReward` | `amount` | выдаются через `GoldRewardHandler` / `ExperienceRewardHandler` |

У всех целей также есть `ObjectiveId` (по нему сохраняется прогресс), `Title`, `Description` и `IsOptional`
(в этой версии только хранится). У каждой награды есть стабильный `RewardId`.

Награды выдаются **только при входе в End (Completed)** — никогда в середине потока.

## Валидация

`QuestGraphValidator.Validate(graph, otherQuests)` возвращает список `QuestDiagnostic` (уровень, код, сообщение, Id узла).
Редактор запускает проверку при открытии задания, по кнопке **Validate** и при **Save**; библиотека помечает невалидные
задания значком ошибки. Граф с ошибками всё равно сохраняется (работа не теряется), но **`QuestManager` откажется его запускать**.

Ошибки (`QE…`): нет Start или их несколько, нет End, связи на несуществующие узлы и порты, несовместимые порты или
превышена capacity, повторяющиеся связи, пустой или повторяющийся `questId`, повторяющиеся Id узлов / целей / наград,
недопустимые значения (`requiredCount ≤ 0`, `amount ≤ 0`, пустой `enemyId` / `itemId`…), неподключённые Flow-порты,
ни один End не достижим из Start, Flow-циклы, отсутствующие классы узлов, награда подключена не к End (Completed).

Предупреждения (`QW…`): узел не достижим из Start, награда ни к чему не подключена, у цели нет описания,
у End (Completed) нет наград. Полный список кодов — в `QuestDiagnosticCodes`.

## Исполнение: QuestManager

`QuestManager` — обычный C#-класс, который создаёт игра. Не MonoBehaviour и не синглтон.

```csharp
using UnityEngine;
using Vokazhira.QuestEditor;

public sealed class GameQuests : MonoBehaviour
{
    [SerializeField] private QuestDatabase database;
    private QuestManager quests;

    private void Awake()
    {
        quests = new QuestManager(database, new FileQuestSaveService());
        quests.RegisterRewardHandler(new GoldRewardHandler(myWallet));            // ICurrencyService вашей игры
        quests.RegisterRewardHandler(new ExperienceRewardHandler(myExperience));  // IExperienceService вашей игры
        quests.QuestCompleted += quest => Debug.Log($"Completed: {quest.QuestId}");
        quests.Load();
    }

    public void OnGoblinKilled() => quests.Report(QuestEvent.EnemyKilled("goblin"));

    private void OnApplicationQuit() => quests.Save();
    private void OnDestroy() => quests.Dispose();
}
```

- **Жизненный цикл:** `CanStart`, `Start`, `Abandon` (→ Failed), `Report`, `Claim`, `GetState`, `GetInstance`, `GetInstances`,
  `GetValidationErrors`.
- **Состояния:** `Available → Active → Claimable → Completed` или `Failed`.
- **Награды:** в режиме `AutoGrant` выдаются сразу при входе в End (Completed); в режиме `ManualClaim` задание становится
  `Claimable`, а награды выдаются по `Claim(questId)`. Если награду выдать не удалось (нет обработчика или он вернул `false`),
  задание остаётся `Claimable`, и следующий `Claim` выдаст только недостающие награды — дважды награда не выдаётся.
- **События:** `QuestStarted`, `QuestProgressChanged`, `QuestClaimable`, `QuestCompleted`, `QuestFailed`,
  `QuestRewardGranted`, `QuestRewardFailed`. `Dispose()` отписывает всех подписчиков и удаляет обработчики.
- **Сервисы:** `ICurrencyService` и `IExperienceService` реализует игра. Для любого другого типа награды нужен свой
  `IRewardHandler<TReward>` (см. ниже). Обработчик ищется сначала по точному типу награды, затем по базовым.
- **Логи:** третий аргумент конструктора — `UnityEngine.ILogger`, куда менеджер пишет сообщения;
  по умолчанию `Debug.unityLogger`.
- События после завершения задания игнорируются; прогресс не превышает требуемый.

Регистрируйте обработчики наград **до** `Load()`: при загрузке задание с уже выполненной целью может сразу продвинуться и выдать награды.

## Сохранение прогресса

Определения заданий — ассеты; прогресс игрока хранится отдельно, в JSON (`JsonUtility`, без `PlayerPrefs`).

- `Capture()` / `Restore(QuestSaveData)` — если у игры своя система сохранений.
- `Save()` / `Load()` работают через `IQuestSaveService`, переданный в конструктор. `FileQuestSaveService` по умолчанию пишет
  в `Application.persistentDataPath/quests.json`; запись атомарная (временный файл, затем замена).
- Нет файла — загружается пустое состояние. Повреждённый или пустой файл не загружается: `Load()` возвращает `false`,
  причина пишется в лог, копия сохраняется как `quests.json.corrupt`.
- После загрузки задание продолжается с сохранённого узла; выданные награды повторно не выдаются.
- Записи заданий, которых нет в базе, хранятся как «осиротевшие» и сохраняются обратно без изменений.
- Задание, которое нельзя продолжить (сохранённого узла больше нет, неизвестное состояние или в графе появились ошибки),
  **замораживается** (`QuestInstance.IsFrozen`): события его не двигают, а запись сохраняется без изменений.
- Сохранение более новой версии формата не загружается; текущее состояние остаётся прежним.

## Свои цели, условия и награды

Тип узла — это `[Serializable]` не generic-класс с публичным конструктором без параметров и атрибутом `[QuestNode]`.
Его можно положить в любое место проекта: редактор найдёт его через `TypeCache` и добавит в меню создания узлов.

**Цель** «поговорить с NPC»:

```csharp
using System;
using UnityEngine;
using Vokazhira.QuestEditor;

[Serializable]
[QuestNode("Objectives/Talk To NPC", "#43A047")]
public sealed class TalkObjective : ObjectiveNodeData
{
    [SerializeField, Tooltip("NPC identifier.")] private string npcId = string.Empty;

    public override int RequiredCount => 1;

    public override bool TryGetProgress(in QuestEvent e, out int delta) =>
        QuestObjectiveMatching.TryMatch(e, "NpcTalked", npcId, false, out delta);

    public override void Validate(QuestValidationContext context)
    {
        base.Validate(context);
        if (string.IsNullOrWhiteSpace(npcId)) context.AddError(QuestDiagnosticCodes.InvalidValue, "npcId is empty.");
    }

    public override string GetSummary() => $"Talk to {npcId}";
}

// В игре: quests.Report(new QuestEvent("NpcTalked", "elder"));
```

**Условие** «другое задание провалено». Условия читают состояния заданий через `IQuestConditionContext`:

```csharp
using System;
using UnityEngine;
using Vokazhira.QuestEditor;

[Serializable]
[QuestNode("Conditions/Quest Failed", "#FB8C00")]
public sealed class QuestFailedCondition : ConditionNodeData
{
    [SerializeField, Tooltip("questId of the quest to check.")] private string questId = string.Empty;

    public override bool Evaluate(IQuestConditionContext context) =>
        context != null && context.GetState(questId) == QuestState.Failed;

    public override void Validate(QuestValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(questId)) context.AddError(QuestDiagnosticCodes.InvalidValue, "questId is empty.");
    }

    public override string GetSummary() => $"Quest '{questId}' failed?";
}
```

**Награда** «выдать предмет» и её обработчик:

```csharp
using System;
using UnityEngine;
using Vokazhira.QuestEditor;

[Serializable]
[QuestNode("Rewards/Item", "#6D4C41")]
public sealed class ItemReward : RewardNodeData
{
    [SerializeField] private string itemId = string.Empty;
    [SerializeField, Min(1)] private int count = 1;

    public string ItemId => itemId;
    public int Count => count;

    public override string GetSummary() => $"+{count} {itemId}";
}

public sealed class ItemRewardHandler : IRewardHandler<ItemReward>
{
    private readonly IInventory inventory;   // инвентарь вашей игры

    public ItemRewardHandler(IInventory inventory) => this.inventory = inventory;

    public bool TryGrant(ItemReward reward, string questId) => inventory.Add(reward.ItemId, reward.Count);
}

// quests.RegisterRewardHandler(new ItemRewardHandler(inventory));
```

## Переименование классов узлов: `[MovedFrom]`

Узлы хранятся через `[SerializeReference]`, а он записывает имя класса, namespace и сборку. Если переименовать или
перенести класс узла, существующие задания потеряют этот узел (валидатор сообщит `QE018`, редактор назовёт отсутствующий
класс). Чтобы данные сохранились, добавьте `[MovedFrom]` со старым расположением:

```csharp
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[QuestNode("Objectives/Talk To NPC")]
[MovedFrom(false, sourceNamespace: "MyGame.Quests", sourceAssembly: "MyGame", sourceClassName: "TalkToNpcObjective")]
public sealed class TalkObjective : ObjectiveNodeData { /* ... */ }
```

Не используйте generic-типы как классы узлов.

## Пример

**Package Manager → Quest Editor → Samples → Basic Quest Example → Import.** В примере два задания («The Elder's Letter»
и «Forest Cleanup» из схемы выше), база заданий и сцена `QuestDemo`: bootstrap, который создаёт `QuestManager` и простые
сервисы золота и опыта, и интерфейс на IMGUI с кнопками «Kill goblin» / «Find letter», прогрессом заданий, Start / Claim /
Abandon, золотом и опытом, сообщениями о событиях и Save / Load / сбросом прогресса.
**Tools → Quest Editor → Demo → Build Demo Assets** пересоздаёт ассеты примера. Пример использует только публичный API.

Золото и опыт в примере не сохраняются: пакет сохраняет только прогресс заданий, а сервисы демо хранят значения в памяти.

## Запуск тестов

В пакете есть EditMode-тесты runtime-логики (`Vokazhira.QuestEditor.Tests.Runtime`) и редактора
(`Vokazhira.QuestEditor.Tests.Editor`).

- **В редакторе:** **Window → General → Test Runner → EditMode → Run All**.
- **Из командной строки** (проект должен быть закрыт в редакторе):

  ```
  "<Unity 2023.1.22f1>/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml -logFile Logs/tests-editmode.log
  ```

Тесты embedded-пакета видны автоматически. Для пакета, установленного по Git URL, добавьте его в `testables`
в `Packages/manifest.json`: `"testables": ["com.vokazhira.quest-editor"]`.

## Ограничения

- У задания один текущий узел: параллельных веток нет, Flow-циклы запрещены.
- Условия видят только состояния заданий (`IQuestConditionContext.GetState`); произвольных выражений и данных игры пока нет.
- Задание запускается только из `Available`; выполненные и проваленные задания не повторяются.
- `IsOptional` у целей хранится, но не используется.
- В редакторе графа нет копирования/вставки узлов и мини-карты.
- Несохранённые изменения в редакторе не переживают перезапуск Unity (Unity предлагает сохранить их при закрытии).
- Нет локализации: тексты в ассетах заданий — обычные строки.
- Одна версия формата сохранения, миграций пока нет.

## План развития

Параллельные цели, новые условия (числа, инвентарь, состояние объектов), окно-отладчик в Play Mode,
проверка заданий при сборке, экспорт и импорт JSON, локализация.

## Лицензия

MIT — см. [LICENSE.md](LICENSE.md).
