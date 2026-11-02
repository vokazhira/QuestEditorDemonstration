# Quest Editor — архитектура

Документ этапа 0. Описывает, **как** устроена реализация спецификации `Docs/QuestEditor_Spec.md`.
Принятые решения с причинами — в `Docs/Decisions.md`.

## 1. Исходное состояние проекта

Проверено по файлам репозитория (Unity Editor не запускался):

| Что | Значение | Источник |
|---|---|---|
| Версия Unity | `2023.1.22f1 (c1416845ad01)` | `ProjectSettings/ProjectVersion.txt` |
| API Compatibility Level | `6` = .NET Standard 2.1 | `ProjectSettings/ProjectSettings.asset` (`apiCompatibilityLevel`) |
| Сериализация ассетов | Force Text (`m_SerializationMode: 2`) | `ProjectSettings/EditorSettings.asset` |
| Enter Play Mode Options | выключены (domain reload при входе в Play Mode) | `EditorSettings.asset` |
| Ввод | старый Input Manager (`activeInputHandler: 0`) | `ProjectSettings.asset` |
| Unity Test Framework | **1.3.9**, транзитивно через `com.unity.feature.development 1.0.2`; NUnit — `com.unity.ext.nunit 2.0.3` | `Packages/packages-lock.json` |
| UI для демо | `com.unity.ugui 1.0.0`, `com.unity.textmeshpro 3.0.6`, модуль `uielements` | `Packages/manifest.json` |
| Прочее | `collab-proxy`, `timeline`, `visualscripting` — к задаче не относятся | `manifest.json` |
| Скрипты | **нет ни одного**: в `Assets/` только `Scenes/SampleScene.unity` | обход `Assets/` |
| Существующие системы событий, валюты, сохранений | **отсутствуют** | — |

Выводы:

- Системы игры, с которыми нужно интегрироваться, нет. `ICurrencyService`, `IExperienceService`
  и т. п. реализуются тестовыми классами в демо (этап 8).
- Test Framework в `manifest.json` прямой зависимостью не указан, но установлен. Пакету
  достаточно этого; в `package.json` пакета зависимость от `com.unity.test-framework` не добавляем
  (см. Decisions.md, D-008).
- `.meta`-файлы в репозитории отслеживаются (`Assets/Scenes.meta` закоммичен). Я их не создаю —
  их нужно закоммитить после того, как Unity их сгенерирует.

## 2. Компоненты

```
┌──────────────────────────── YourName.QuestEditor.Editor (Editor only) ───────────────────────────┐
│                                                                                                   │
│  QuestEditorWindow ── Toolbar ── QuestSettingsPopup                                               │
│        │                                                                                          │
│        ├── QuestLibraryView ◄── QuestLibraryPostprocessor (AssetPostprocessor → delayCall)        │
│        ├── QuestGraphView : GraphView ── QuestNodeView : Node (один на все типы)                 │
│        │        └── QuestNodeSearchProvider : ISearchWindowProvider ◄── QuestNodeTypeCache       │
│        ├── QuestNodeInspector (PropertyField → nodes.Array.data[i])                               │
│        └── QuestDiagnosticsView                                                                    │
│                                                                                                   │
│  QuestGraphEditing (все изменения рабочей копии: Undo.RecordObject + методы модели)              │
│  QuestDatabaseEditor (кнопка «Собрать все квесты проекта»)                                         │
└───────────────────────────────────────────────┬───────────────────────────────────────────────────┘
                                                │ ссылается
┌───────────────────────────────────────────────▼──── YourName.QuestEditor (Runtime) ──────────────┐
│ Graph:       QuestGraphAsset, QuestNodeData, QuestEdgeData, QuestPortDefinition, QuestPortRules,  │
│              QuestNodeAttribute                                                                    │
│ Nodes:       Start / End / Objective* / Condition* / Reward*                                       │
│ Validation:  QuestGraphValidator → List<QuestDiagnostic>                                           │
│ Execution:   QuestManager ─┬─ QuestGraphRunner (переходы по графу, чистая логика)                 │
│                            ├─ QuestRewardDispatcher (IRewardHandler<T>)                           │
│                            └─ QuestInstance (состояние одного квеста)                              │
│              QuestDatabase (SO, явный список ассетов), QuestEvent                                  │
│ Services:    ICurrencyService, IExperienceService, IRewardHandler<T>, GoldRewardHandler, ...       │
│ Persistence: QuestSaveData (DTO), IQuestSaveService, FileQuestSaveService                          │
└───────────────────────────────────────────────────────────────────────────────────────────────────┘
                                                ▲ создаёт и вызывает
                                   Игра / демо (bootstrap MonoBehaviour)
```

Ключевые принципы:

1. **Модель — единственный источник правды.** `QuestGraphAsset` не знает про UI. Все view
   (узлы, связи, инспектор, диагностика) пересобираются из модели в любой момент.
2. **Редактор правит рабочую копию**, а не ассет. Save переносит копию в ассет.
3. **Runtime исполняет тот же ассет** без отдельного «скомпилированного» формата и только
   после успешной валидации.
4. **Расширяемость без правок пакета:** новый тип узла — это класс-наследник
   `ObjectiveNodeData` / `ConditionNodeData` / `RewardNodeData` с атрибутом `[QuestNode]` в любой
   сборке пользователя. Редактор находит его через `TypeCache`, runtime работает с ним полиморфно,
   награды — через зарегистрированный `IRewardHandler<T>`.
5. **Прогресс игрока отдельно от определений**: `QuestInstance` — обычный C#-объект, сохраняется
   в JSON-файл. ScriptableObject'ы в рантайме только читаются.

## 3. Зависимости сборок

| Сборка | Платформы | Ссылки (по имени) | Назначение |
|---|---|---|---|
| `YourName.QuestEditor` | все | — | Модель, валидатор, исполнение, сохранения |
| `YourName.QuestEditor.Editor` | Editor | `YourName.QuestEditor` | Окно, GraphView, инспектор, библиотека |
| `YourName.QuestEditor.Tests.Runtime` | Editor | `YourName.QuestEditor`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `nunit.framework.dll`; `defineConstraints: UNITY_INCLUDE_TESTS` | EditMode-тесты runtime-логики. **Не** ссылается на Editor-сборку пакета → тесты подтверждают, что логика работает без редакторского кода |
| `YourName.QuestEditor.Tests.Editor` | Editor | обе сборки пакета + TestRunner; `nunit.framework.dll`; `UNITY_INCLUDE_TESTS` | Тесты, которым нужен `UnityEditor` (AssetDatabase, `CopySerialized`, рабочая копия, утилиты редактора) |

Почему тестовая «Runtime»-сборка тоже Editor-only — см. D-004.

Runtime-сборка не ссылается на `UnityEditor` ни в каком виде: ни `#if UNITY_EDITOR`-блоков
с редакторским API, ни ссылок в asmdef.

## 4. Модель данных (Runtime)

```
QuestGraphAsset : ScriptableObject
  string questId, displayName, description
  int formatVersion                         (текущая = 1)
  QuestRewardMode rewardMode                (AutoGrant | ManualClaim)
  [SerializeReference] List<QuestNodeData> nodes
  List<QuestEdgeData> edges
  QuestNodeData FindNode(string id)         (ленивый Dictionary, сбрасывается при изменениях и в OnValidate)

[Serializable] abstract QuestNodeData
  string id (GUID, readonly-доступ), Vector2 position
  abstract IReadOnlyList<QuestPortDefinition> Ports
  virtual void Validate(QuestValidationContext ctx)
  virtual string GetSummary()

  StartNodeData
  abstract ObjectiveNodeData   ObjectiveId, Title, Description, IsOptional, RequiredCount,
                               abstract bool TryGetProgress(in QuestEvent e, out int delta)
      KillObjective, CollectObjective, CompleteWaveObjective
  abstract ConditionNodeData   abstract bool Evaluate(IQuestConditionContext ctx)
      QuestCompletedCondition
  abstract RewardNodeData      RewardId
      GoldReward, ExperienceReward
  EndNodeData                  QuestEndResult result (Completed | Failed)

QuestEdgeData       fromNodeId, fromPortId, toNodeId, toPortId
QuestPortDefinition portId, direction (Input/Output), kind (Flow/Reward), capacity (Single/Multi), displayName
QuestPortRules      static bool CanConnect(...) — общая логика для GraphView и валидатора
QuestNodeAttribute  (string menuPath, string colorHex)
```

Стабильные id портов (константы в `QuestPortIds`): `in`, `out`, `true`, `false`, `reward`, `rewards`.
Связи хранят только id — переименование подписи порта данных не ломает.

`Ports` — вычисляемое свойство (не сериализуется). У `EndNodeData` набор портов зависит от
`result`: порт `rewards` есть только у `Completed`. Если у End с наградами переключить результат
на `Failed`, связи наград станут «висящими» — это ловит валидатор (ошибка 3/11), а редактор
при смене результата предлагает удалить такие связи (этап 4).

## 5. Публичный Runtime API

```csharp
// События игры
public readonly struct QuestEvent { Type; TargetId; Amount; EnemyKilled(...); ItemCollected(...); WaveCompleted(...); }
public static class QuestEventTypes { const string EnemyKilled, ItemCollected, WaveCompleted; }

// Состояния
public enum QuestState { Available, Active, Claimable, Completed, Failed }

// Менеджер (обычный C#-класс, создаётся игрой)
public sealed class QuestManager : IQuestConditionContext, IDisposable
{
    public QuestManager(QuestDatabase database, IQuestSaveService saveService);

    public QuestState GetState(string questId);
    public bool CanStart(string questId);              // Available + граф валиден
    public bool Start(string questId);
    public bool Abandon(string questId);               // Active → Failed
    public void Report(in QuestEvent e);
    public bool Claim(string questId);                 // Claimable → Completed (если все награды выданы)
    public IReadOnlyList<IQuestInstanceView> GetInstances();

    public QuestSaveData Capture();
    public void Restore(QuestSaveData data);
    public void Save();                                // Capture → saveService.Save
    public bool Load(out string error);                // saveService.TryLoad → Restore

    public void RegisterRewardHandler<TReward>(IRewardHandler<TReward> handler) where TReward : RewardNodeData;

    public event Action<string> QuestStarted;
    public event Action<string, string, int, int> QuestProgressChanged; // questId, objectiveId, current, required
    public event Action<string> QuestCompleted;
    public event Action<string> QuestFailed;
    public event Action<string, RewardNodeData> QuestRewardGranted;
    public event Action<string, RewardNodeData, string> QuestRewardFailed; // ..., причина

    public void Dispose();                             // очищает подписчиков и обработчики
}

// Награды и сервисы игры
public interface IRewardHandler<in TReward> where TReward : RewardNodeData { bool TryGrant(TReward reward, string questId); }
public interface ICurrencyService   { bool AddGold(int amount); }
public interface IExperienceService { bool AddExperience(int amount); }
public sealed class GoldRewardHandler       : IRewardHandler<GoldReward>       { public GoldRewardHandler(ICurrencyService s); }
public sealed class ExperienceRewardHandler : IRewardHandler<ExperienceReward> { public ExperienceRewardHandler(IExperienceService s); }

// Условия
public interface IQuestConditionContext { QuestState GetState(string questId); }

// Валидация (используется и редактором)
public static class QuestGraphValidator
{
    public static List<QuestDiagnostic> Validate(QuestGraphAsset graph, IReadOnlyList<QuestGraphAsset> otherQuests = null);
}
public readonly struct QuestDiagnostic { Severity; Code; Message; NodeId; }

// Сохранения
public interface IQuestSaveService { bool TryLoad(out QuestSaveData data, out string error); void Save(QuestSaveData data); }
public sealed class FileQuestSaveService : IQuestSaveService { public FileQuestSaveService(string fileName = "quests.json"); }
```

Сигнатуры событий и мелкие детали могут уточниться на этапе 6 — изменения будут записаны в
Decisions.md. `QuestGraphRunner`, `QuestRewardDispatcher`, `QuestInstance` — `internal` либо
публичные, но не нужные игре напрямую (решу на этапе 6, тесты получат доступ через
`InternalsVisibleTo`, если понадобится).

### Исполнение (кратко)

- У экземпляра квеста ровно один текущий узел (параллельных веток нет).
- `QuestGraphRunner.Advance` проходит мгновенные узлы (Start, Condition) в цикле до первого
  «устойчивого» узла: Objective (ждёт событий) или End. Защита от зацикливания — лимит шагов
  (циклы и так запрещены валидатором).
- В сохранение попадает только устойчивый узел, поэтому после загрузки Condition заново не
  вычисляется.
- `Report` рассылает событие всем `Active`-экземплярам в порядке списка `QuestDatabase`.
- Награды выдаются только при входе в End(Completed): `AutoGrant` — сразу, `ManualClaim` — при
  `Claim`. Выданные `RewardId` хранятся в экземпляре и никогда не выдаются повторно.

## 6. Редактор (кратко)

- `QuestEditorWindow` хранит в `[SerializeField]`: GUID открытого ассета, ссылку на рабочую копию,
  флаг несохранённых изменений. `CreateGUI()` полностью пересобирает UI из этих данных →
  окно переживает domain reload.
- Рабочая копия: `Instantiate(asset)` + `HideFlags.DontSave`. Save: `EditorUtility.CopySerialized`
  → `SetDirty` → `AssetDatabase.SaveAssetIfDirty`. Корректность переноса `[SerializeReference]`
  проверяется тестом (этап 3a; см. риски).
- Все правки модели — через `QuestGraphEditing`: `Undo.RecordObject(workingCopy, ...)` + изменение
  + пометка «изменено». `Undo.undoRedoPerformed` → полная пересборка графа, инспектора, диагностики.
- `QuestGraphView` синхронизируется с моделью только через `graphViewChanged`
  (перемещения, создание и удаление связей, удаление узлов) и `nodeCreationRequest`.
- Инспектор: один `PropertyField` на `nodes.Array.data[i]` из `SerializedObject` рабочей копии.
  После структурных изменений списка `nodes` (добавление/удаление/Undo) — повторная привязка,
  т. к. индексы сдвигаются.

## 7. Файлы по этапам

Пути относительно `Packages/com.yourname.quest-editor/`. Один класс — один файл. Список
ориентировочный: файл, который окажется не нужен, не создаётся.

**Этап 1 — каркас**
- `package.json`, `README.md` (заглушка)
- `Runtime/YourName.QuestEditor.asmdef`
- `Editor/YourName.QuestEditor.Editor.asmdef`
- `Tests/Runtime/YourName.QuestEditor.Tests.Runtime.asmdef`
- `Tests/Editor/YourName.QuestEditor.Tests.Editor.asmdef`
- по одному минимальному `.cs` в каждой сборке не создаю (пустые классы запрещены); asmdef без
  скриптов Unity допускает — сборка просто не компилируется, пока нет кода

**Этап 2 — модель и правила портов**
- `Runtime/Graph/`: `QuestGraphAsset`, `QuestNodeData`, `QuestEdgeData`, `QuestPortDefinition`,
  `QuestPortDirection`, `QuestPortKind`, `QuestPortCapacity`, `QuestPortIds`, `QuestPortRules`,
  `QuestNodeAttribute`, `QuestRewardMode`, `QuestValidationContext` (минимальный, расширяется на этапе 5)
- `Runtime/Nodes/`: `StartNodeData`, `EndNodeData`, `QuestEndResult`
- `Runtime/Nodes/Objectives/`: `ObjectiveNodeData`, `KillObjective`, `CollectObjective`, `CompleteWaveObjective`
- `Runtime/Nodes/Conditions/`: `ConditionNodeData`, `QuestCompletedCondition`
- `Runtime/Nodes/Rewards/`: `RewardNodeData`, `GoldReward`, `ExperienceReward`
- `Runtime/Execution/`: `QuestEvent`, `QuestEventTypes`, `QuestState`, `IQuestConditionContext`
  (нужны сигнатурам `TryGetProgress` и `Evaluate`)
- `Tests/Runtime/`: `QuestGraphAssetTests`, `QuestPortRulesTests`
- `Tests/Editor/`: `QuestGraphAssetSerializationTests` (сохранение/перезагрузка ассета с `[SerializeReference]`)

**Этап 3a — окно и библиотека**
- `Editor/Window/`: `QuestEditorWindow`, `QuestEditorToolbar`, `QuestSettingsPopup`, `QuestWorkingCopy`
- `Editor/Library/`: `QuestLibraryView`, `QuestLibraryPostprocessor`, `QuestAssetUtility` (Create/Duplicate/Delete)
- `Editor/Window/QuestEditor.uss`
- `Tests/Editor/`: `QuestWorkingCopyTests` (в т. ч. `CopySerialized` + `[SerializeReference]`)

**Этап 3b — холст**
- `Editor/GraphView/`: `QuestGraphView`, `QuestNodeView`, `QuestNodeSearchProvider`, `QuestNodeTypeCache`
- `Editor/Utilities/QuestGraphEditing`
- `Editor/GraphView/QuestGraphView.uss`

**Этап 3c — связи и Undo**
- `Editor/GraphView/`: доработка `QuestGraphView` (edges, `GetCompatiblePorts`), `QuestEdgeView` — только если понадобится
- `Tests/Editor/QuestGraphEditingTests` (Undo + пересборка из модели)

**Этап 4 — инспектор**
- `Editor/Inspector/QuestNodeInspector`, доработка `QuestSettingsPopup`

**Этап 5 — валидация**
- `Runtime/Validation/`: `QuestGraphValidator`, `QuestDiagnostic`, `QuestDiagnosticSeverity`,
  `QuestDiagnosticCodes`, полноценный `QuestValidationContext`
- `Editor/Window/QuestDiagnosticsView`
- `Tests/Runtime/QuestGraphValidatorTests` (тест на каждое правило раздела 4)

**Этап 6 — runtime**
- `Runtime/Execution/`: `QuestInstance`, `QuestGraphRunner`, `QuestManager`, `QuestRewardDispatcher`, `QuestDatabase`
- `Runtime/Services/`: `IRewardHandler`, `ICurrencyService`, `IExperienceService`, `GoldRewardHandler`, `ExperienceRewardHandler`
- `Editor/Utilities/QuestDatabaseEditor`
- `Tests/Runtime/`: `QuestManagerTests`, `QuestGraphRunnerTests`, `QuestRewardDispatcherTests`, тестовые фейки сервисов

**Этап 7 — сохранение**
- `Runtime/Persistence/`: `QuestSaveData`, `QuestInstanceSaveData`, `ObjectiveProgressEntry`,
  `IQuestSaveService`, `FileQuestSaveService`
- `Tests/Runtime/`: `QuestPersistenceTests`, `FileQuestSaveServiceTests`

**Этап 8 — демо** (`Assets/QuestEditorDemo/`)
- сцена, `QuestDemoBootstrap`, `DemoCurrencyService`, `DemoExperienceService`, `QuestDemoUI`,
  `QuestDatabase`-ассет и пример квеста «Зачистка леса»; затем перенос в `Samples~/BasicQuestExample/`

**Этап 9 — документация**: `README.md`, `CHANGELOG.md`, `LICENSE`.

## 8. Риски

### GraphView в 2023.1

| Риск | Что делаем |
|---|---|
| Пространство имён `Experimental`: API без гарантий, документация скудная | Используем только базовый набор (`GraphView`, `Node`, `Port`, `Edge`, `SearchWindow`, манипуляторы); вся логика в модели, GraphView — тонкий слой, который можно заменить |
| `GridBackground` без USS невидим | Отдельный USS с `--grid-background-color`, `--line-color`, `--thick-line-color`, `--spacing` |
| GraphView не интегрирован с Undo | Undo записываем сами в `graphViewChanged`; на `undoRedoPerformed` граф пересобирается целиком из модели |
| `graphViewChanged.edgesToCreate`: отклонение связи | Убираем недопустимые связи из `edgesToCreate`, показываем `ShowNotification` с причиной из `QuestPortRules` |
| Удаление через Delete/Cut идёт через `elementsToRemove` | Защита Start — удаляем его из списка `elementsToRemove` (D-006) |
| `SearchWindow` даёт экранные координаты | Перевод: `screenMousePosition − window.position.position` → `contentViewContainer.WorldToLocal` |
| Пересборка всего графа на каждый Undo может мигать/тормозить на больших графах | Для MVP приемлемо (графы квестов небольшие); сохраняем и восстанавливаем `viewTransform` при пересборке |
| Copy/Paste и Duplicate по умолчанию у GraphView | В MVP отключаем (`serializeGraphElements`/`canPasteSerializedData` не реализуем, `canPaste` = false) |

### `[SerializeReference]` в 2023.1

| Риск | Что делаем |
|---|---|
| Переименование/перенос класса узла ломает данные | `[MovedFrom]` при любом рефакторинге; правило в CLAUDE.md и README |
| Класс узла удалён → «missing type» | Валидатор: `SerializationUtility.HasManagedReferencesWithMissingTypes` (ошибка 10); такой граф не исполняется. Узел с отсутствующим типом в списке будет `null` — модель, view и валидатор обязаны это переживать |
| `EditorUtility.CopySerialized` и managed references | Тест на этапе 3a. Если перенос некорректен — альтернатива: `EditorJsonUtility.ToJson` + `FromJsonOverwrite` (поддерживает `[SerializeReference]`) или копирование через `SerializedObject` |
| `PropertyField` по пути `nodes.Array.data[i]` указывает на индекс, а не на узел | Перепривязка инспектора после любого структурного изменения и Undo; поиск индекса по `id` узла |
| Generic-типы в `[SerializeReference]` | Не используем (правило CLAUDE.md) |
| Один и тот же объект в списке дважды (shared reference) | Узлы всегда создаются через `new`; при дублировании ассета (`AssetDatabase.CopyAsset`) ссылки остаются раздельными |
| Ленивый `Dictionary` узлов устаревает после Undo / изменения через `SerializedObject` | Сброс кэша в `OnValidate`, после Undo и в методах модели; при промахе — повторное построение |

### Прочее

| Риск | Что делаем |
|---|---|
| Тесты встроенного (embedded) пакета могут не появиться в Test Runner без `testables` в `manifest.json` | Проверка на этапе 1 вашими руками. Если тестов не видно — попрошу разрешения добавить `"testables": ["com.yourname.quest-editor"]` в `manifest.json` |
| Я не могу запустить Unity в этой среде (Linux-контейнер, Unity нет) | Компиляцию и тесты подтверждаете вы; в отчётах это прямо указано |
| Имя пакета и namespace `YourName` — заглушки; переименование после появления ассетов сломает `[SerializeReference]`-данные | Вопрос задан до этапа 1 |
