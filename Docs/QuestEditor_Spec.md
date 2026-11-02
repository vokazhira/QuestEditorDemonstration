# Спецификация: Quest Editor для Unity 2023.1.22f1

> Общие правила работы, окружение и запреты — в `CLAUDE.md`. Этот документ описывает,
> **что** строим. Выполняй этапы из раздела 10 по одному и останавливайся после каждого.

## 1. Цель

Ты — Senior Unity Tools Engineer и C# Architect. Делаем **переиспользуемый UPM-пакет**:
визуальный редактор заданий на графе + runtime-система их исполнения.

Разработчик открывает окно `Tools → Quest Editor`, собирает задание из узлов, соединяет их,
настраивает параметры, сохраняет в ассет — и это задание исполняется в игре без Editor API.

Пример задания «Зачистка леса»:

```
[Start] → [Kill: goblin ×10] → [Condition: квест "intro" выполнен?] ─True→ [End: Completed] ← [Reward: 100 gold]
                                                                     │                       ← [Reward: 25 xp]
                                                                     └False→ [End: Failed]
```

Задача первой версии — **рабочий MVP с качественной архитектурой**, а не максимум функций.
Архитектура должна позволять добавлять новые типы целей, условий и наград **без изменения
кода пакета** — это главный критерий переиспользуемости.

## 2. Модель графа

### 2.1. Типы узлов и порты

Порты бывают двух видов: **Flow** (порядок исполнения) и **Reward** (подключение наград к финалу).

| Узел | Входы | Выходы | Назначение |
|---|---|---|---|
| **Start** | — | Flow (single) | Начало. Ровно один на граф |
| **Objective** | Flow (multi) | Flow (single) | Цель с прогрессом. Ждёт событий, пока прогресс < требуемого |
| **Condition** | Flow (multi) | `True` Flow (single), `False` Flow (single) | Мгновенная проверка и ветвление |
| **Reward** | — | Reward (single) | Описание награды. Не участвует в Flow |
| **End** | Flow (multi), `Rewards` Reward (multi) | — | Финал с результатом `Completed` или `Failed`. Порт `Rewards` есть только у `Completed` |

Правила соединений (общий класс `QuestPortRules` в **Runtime**-сборке, им пользуются и редактор,
и валидатор):

- только Output → Input, только порты одного вида (Flow ↔ Flow, Reward ↔ Reward);
- нельзя соединять узел сам с собой;
- соблюдается capacity порта (single/multi);
- циклы по Flow запрещены (проверяет валидатор; редактор показывает ошибку).

Награды выдаются **только при входе в End(Completed)** — никогда в середине потока.
Это исключает выдачу награды до смены состояния квеста.

### 2.2. Встроенные типы для MVP

- **Objectives:** `KillObjective` (enemyId, requiredCount), `CollectObjective` (itemId, requiredCount),
  `CompleteWaveObjective` (waveId или пусто = любая, requiredCount).
  Общие поля: ObjectiveId, Title, Description (для UI), IsOptional (в MVP только хранится).
- **Conditions:** `QuestCompletedCondition` (questId другого задания).
  Задел на будущее (не реализовывать): сравнение чисел, наличие предмета, состояние объекта.
  Никаких вычислителей произвольных выражений.
- **Rewards:** `GoldReward` (amount), `ExperienceReward` (amount). У каждой награды стабильный RewardId.
- **End:** `Completed` / `Failed`.

### 2.3. Классы данных (Runtime)

- `QuestGraphAsset : ScriptableObject`
  - `questId` (string, уникальный), `displayName`, `description`;
  - `formatVersion` (int) — для будущих миграций;
  - `rewardMode`: `AutoGrant` | `ManualClaim`;
  - `[SerializeReference] List<QuestNodeData> nodes`;
  - `List<QuestEdgeData> edges`;
  - поиск узла по Id — через метод, который строит словарь лениво (Dictionary **не сериализуется**).
- `QuestNodeData` — абстрактный `[Serializable]` базовый класс:
  - `id` (GUID-строка, генерируется при создании, неизменяемая), `position` (Vector2);
  - `abstract IReadOnlyList<QuestPortDefinition> Ports` — описание портов;
  - `virtual void Validate(QuestValidationContext ctx)` — проверки собственных полей;
  - `virtual string GetSummary()` — короткий текст для заголовка узла («Убить goblin ×10»).
- Подклассы: `StartNodeData`, абстрактный `ObjectiveNodeData`, абстрактный `ConditionNodeData`,
  абстрактный `RewardNodeData`, `EndNodeData` + конкретные типы из 2.2.
  - `ObjectiveNodeData`: `abstract bool TryGetProgress(in QuestEvent e, out int delta)` и `RequiredCount`.
    Чистая функция без сервисов.
  - `ConditionNodeData`: `abstract bool Evaluate(IQuestConditionContext context)`.
- `QuestPortDefinition` — `portId` (стабильная строка), direction, kind (Flow/Reward), capacity, displayName.
- `QuestEdgeData` — `fromNodeId`, `fromPortId`, `toNodeId`, `toPortId`. Только идентификаторы.
- Атрибут `[QuestNode("Категория/Имя", color)]` на конкретных типах — для меню создания узлов.

В ассете не хранятся ссылки на элементы GraphView. Модель не знает об UI.

### 2.4. Где живёт граница Editor / Runtime

Runtime **исполняет тот же `QuestGraphAsset`**, отдельного «скомпилированного» формата нет
(не дублируем данные без пользы). `QuestGraphValidator` находится в Runtime-сборке:

- редактор вызывает его при Validate и при Save и показывает результат;
- Save сохраняет даже граф с ошибками (работа не теряется), но в библиотеке такой граф помечен как невалидный;
- `QuestManager` валидирует граф при регистрации и **отказывается запускать** граф с ошибками (лог + причина).

Это и есть «публикация»: исполнять можно только граф без ошибок уровня Error.

## 3. Quest Editor Window (Editor)

Меню `Tools → Quest Editor`. Двойной клик по `QuestGraphAsset` в Project открывает его в окне (`[OnOpenAsset]`).
UI собирается в коде + USS (см. ограничения UI Toolkit в `CLAUDE.md`).

### 3.1. Редактирование и сохранение

- Окно редактирует **рабочую копию** ассета (`ScriptableObject.Instantiate`, `HideFlags.DontSave`).
  Save копирует её в ассет (`EditorUtility.CopySerialized` → `SetDirty` → `AssetDatabase.SaveAssetIfDirty`).
  Discard просто выбрасывает копию. Проверь тестом, что `CopySerialized` корректно переносит
  `[SerializeReference]`-узлы в 2023.1; если нет — предложи альтернативу.
- Все изменения рабочей копии проходят через `Undo` → Ctrl+Z / Ctrl+Y работают.
  На `Undo.undoRedoPerformed` граф пересобирается из модели.
- Несохранённые изменения: `EditorWindow.hasUnsavedChanges` + `saveChangesMessage` + `SaveChanges()`.
  При New/Open/закрытии окна с несохранёнными изменениями — диалог Save / Discard / Cancel.

### 3.2. Раскладка

- **Верхняя панель:** имя текущего задания, New, Open, Save, Save As, Validate, индикатор `*`
  несохранённых изменений, кнопка настроек задания (questId, displayName, description, rewardMode).
- **Слева — библиотека:** список всех `QuestGraphAsset` (`AssetDatabase.FindAssets("t:QuestGraphAsset")`).
  Обновление: при открытии окна, по кнопке Refresh и по `AssetPostprocessor` (с отложенным вызовом через
  `EditorApplication.delayCall`, без сканирования каждый кадр). Поиск по названию и questId,
  иконка ошибки у невалидных, Create / Duplicate (с новым questId) / Delete (с подтверждением).
- **Центр — граф** (`QuestGraphView : GraphView`):
  `GridBackground` (стилизуй через USS, иначе он невидим), `ContentZoomer`, `ContentDragger`,
  `SelectionDragger`, `RectangleSelector`.
  - Создание узлов: контекстное меню + `nodeCreationRequest` → `SearchWindow` с `ISearchWindowProvider`.
    Список типов — через `TypeCache.GetTypesDerivedFrom<QuestNodeData>()` с атрибутом `[QuestNode]`
    (это Editor-only и дёшево; ручная регистрация не нужна). Новый узел создаётся в точке курсора и выделяется.
  - `GetCompatiblePorts` использует `QuestPortRules`.
  - Синхронизация с моделью через `graphViewChanged`: перемещения (позиции), созданные и удалённые
    связи, удалённые узлы (вместе с их связями). Недопустимые связи отклоняются с сообщением.
  - Один универсальный `QuestNodeView : Node`: порты строятся из `Ports`, цвет — из атрибута,
    заголовок — `GetSummary()`. Отдельный view для каждого типа не создаётся.
  - Узел Start удалить нельзя, если он единственный (или предупреждение — на твоё усмотрение, задокументируй).
- **Справа — инспектор узла:** `PropertyField`, привязанный к `SerializedObject` рабочей копии
  по пути `nodes.Array.data[i]` (managed reference). Один универсальный инспектор для всех типов;
  тултипы из `[Tooltip]`. После изменения обновляются заголовок узла и индикатор `*`.
- **Снизу — панель диагностики:** уровень (Error/Warning/Info), код, описание.
  Клик — выделить узел и `FrameSelection()`.

Приоритет MVP — стабильность и сохранность данных, а не анимации и украшения.

## 4. Валидация

`QuestGraphValidator` — Runtime-класс без зависимостей от UI. Принимает `QuestGraphAsset`
(и, опционально, список других квестов для проверки уникальности questId) и возвращает
`List<QuestDiagnostic>` (severity, code, message, nodeId?).

Ошибки (Error, блокируют запуск):

1. Нет Start или их больше одного.
2. Нет ни одного End.
3. Связь ссылается на несуществующий узел или порт.
4. Несовместимые порты (по `QuestPortRules`) или превышена capacity.
5. Пустой или повторяющийся questId (повтор — среди всех ассетов проекта, проверяет редактор).
6. Повторяющиеся Id узлов, ObjectiveId или RewardId.
7. Недопустимые значения: requiredCount ≤ 0, amount ≤ 0, пустой enemyId/itemId.
8. Обязательный Flow-вход или выход не подключён (например, у Objective нет выхода).
9. End не достижим из Start; есть Flow-цикл.
10. Тип узла отсутствует (`SerializationUtility.HasManagedReferencesWithMissingTypes`) — например, класс удалили.
11. Reward подключён не к End(Completed).

Предупреждения (Warning, не блокируют): узел не достижим из Start, Reward ни к чему не подключён,
пустое Description у цели, у End(Completed) нет наград.

Проверка «есть ли обработчик для типа награды» выполняется в `QuestManager` при старте квеста
(в редакторе сервисов игры нет).

## 5. Runtime

### 5.1. События игры

```csharp
public readonly struct QuestEvent
{
    public readonly string Type;      // константы в QuestEventTypes
    public readonly string TargetId;
    public readonly int Amount;
    public static QuestEvent EnemyKilled(string enemyId, int amount = 1);
    public static QuestEvent ItemCollected(string itemId, int amount = 1);
    public static QuestEvent WaveCompleted(string waveId, int amount = 1);
}
```

Игра сообщает о событиях одним методом: `questManager.Report(QuestEvent.EnemyKilled("goblin"))`.
Пользовательские события — свой `Type` + своя `ObjectiveNodeData`.

### 5.2. Состояния и QuestInstance

`QuestState`: `Available`, `Active`, `Claimable`, `Completed`, `Failed`.

`QuestInstance` (обычный C#-класс; не изменяет ассет): questId, state, currentNodeId,
прогресс по ObjectiveId, множество выданных RewardId.

Исполнение:

- `Start` → сразу переход по Flow.
- `Objective` → ждёт `QuestEvent`; при `progress >= RequiredCount` — переход. Прогресс не превышает требуемый.
- `Condition` → `Evaluate` сразу, переход по `True` или `False`.
- `End(Failed)` → `Failed`.
- `End(Completed)`:
  - `AutoGrant` → выдать награды → `Completed`;
  - `ManualClaim` → `Claimable`; награды выдаются при `Claim(questId)` → `Completed`.
  - Если хотя бы одна награда не выдана (нет обработчика или он вернул false) — квест остаётся
    `Claimable`, невыданные награды можно получить повторным `Claim`. Уже выданные RewardId не выдаются повторно.
- События после завершения квеста игнорируются.

### 5.3. QuestManager и сервисы

`QuestManager` — обычный C#-класс, создаётся игрой: `new QuestManager(QuestDatabase database, IQuestSaveService saveService)`.
Не MonoBehaviour и не синглтон. Отвечает только за жизненный цикл и маршрутизацию событий;
исполнение графа вынеси в `QuestGraphRunner`, выдачу наград — в `QuestRewardDispatcher`.

- `QuestDatabase : ScriptableObject` — явный список `QuestGraphAsset`. В редакторе — кнопка
  «Собрать все квесты проекта». Никакого `Resources.Load`.
- API: `GetState`, `CanStart`, `Start`, `Abandon` (→ Failed), `Report`, `Claim`, `GetInstances`, `Capture` / `Restore`.
- Награды: `RegisterRewardHandler<TReward>(IRewardHandler<TReward> handler) where TReward : RewardNodeData`,
  `bool TryGrant(TReward reward, string questId)`.
  Встроенные обработчики `GoldRewardHandler(ICurrencyService)` и `ExperienceRewardHandler(IExperienceService)`.
  Интерфейсы `ICurrencyService` и `IExperienceService` реализует игра.
- Уведомления (C# events): `QuestStarted`, `QuestProgressChanged`, `QuestCompleted`, `QuestFailed`,
  `QuestRewardGranted`, `QuestRewardFailed`. Есть `Dispose()`, который очищает подписчиков и обработчики.
- `IQuestConditionContext` (реализует `QuestManager`) — даёт условиям доступ к состояниям других квестов.

## 6. Сохранение прогресса

Определение квеста — ассет. Прогресс — отдельно, JSON через `JsonUtility`.

- DTO: `QuestSaveData { int saveVersion; List<QuestInstanceSaveData> quests; }`,
  `QuestInstanceSaveData { questId; state; currentNodeId; List<ObjectiveProgressEntry> progress; List<string> grantedRewardIds; }`.
- `IQuestSaveService { bool TryLoad(out QuestSaveData data, out string error); void Save(QuestSaveData data); }`
- `FileQuestSaveService`: файл в `Application.persistentDataPath`, **атомарная запись** (временный файл → замена).
- Обработай: отсутствующий файл (пустое состояние), повреждённый JSON (не падать, сохранить копию `.corrupt`,
  вернуть ошибку), несовпадение `saveVersion`, отсутствующий квест (запись не теряется — переносится
  в список «осиротевших» и сохраняется обратно), отсутствующий `currentNodeId` (квест не исполняется,
  диагностика в лог; выбранную стратегию задокументируй).
- После загрузки исполнение продолжается с сохранённого узла и прогресса, а не с начала.
  RewardId из `grantedRewardIds` никогда не выдаются повторно.
- Без PlayerPrefs. Граф редактора в сохранение не попадает.

## 7. Структура пакета

```
Packages/com.vokazhira.quest-editor/
├── package.json            ("unity": "2023.1"; зависимости — только подтверждённые)
├── README.md, CHANGELOG.md, LICENSE
├── Runtime/   Vokazhira.QuestEditor.asmdef
│   ├── Graph/          QuestGraphAsset, QuestNodeData, QuestEdgeData, ports, rules
│   ├── Nodes/          Start, End, Objectives/, Conditions/, Rewards/
│   ├── Validation/     QuestGraphValidator, QuestDiagnostic
│   ├── Execution/      QuestManager, QuestInstance, QuestGraphRunner, QuestRewardDispatcher, QuestEvent
│   ├── Services/       ICurrencyService, IExperienceService, IRewardHandler
│   └── Persistence/    DTO, IQuestSaveService, FileQuestSaveService
├── Editor/    Vokazhira.QuestEditor.Editor.asmdef
│   ├── Window/  GraphView/  Inspector/  Library/  Utilities/
├── Tests/
│   ├── Editor/   Vokazhira.QuestEditor.Tests.Editor.asmdef
│   └── Runtime/  Vokazhira.QuestEditor.Tests.Runtime.asmdef
└── Samples~/BasicQuestExample/
```

Это ориентир: не создавай пустых папок и классов ради структуры.

## 8. Usability

Понятные названия и цвета типов узлов, тултипы, контекстное меню, автовыделение нового узла,
подтверждение опасных операций, понятные сообщения об ошибках, переход к ошибочному узлу,
сохранение раскладки, Undo/Redo.

## 9. Что НЕ делать в MVP

Редактор диалогов, RPG-система, сеть, серверное хранение, произвольные выражения в условиях,
циклические графы, параллельные ветки исполнения, локализация, DI-контейнеры, моддинг,
визуальные эффекты, копирование и вставка узлов, MiniMap. Архитектура должна позволять это
добавить потом, но сейчас — нет.

## 10. Этапы

После **каждого** этапа — отчёт и остановка (см. `CLAUDE.md`).

**Этап 0. Анализ и архитектура (без кода).**
Изучи проект: подтверди версию Unity (`ProjectSettings/ProjectVersion.txt`), установленные пакеты
(`Packages/manifest.json`), наличие Unity Test Framework, существующие системы (события, валюта,
сохранения). Создай `Docs/Architecture.md`: схема компонентов, публичный Runtime API, зависимости сборок,
список файлов по этапам, риски GraphView и `[SerializeReference]` в 2023.1. Создай `Docs/Decisions.md`.

**Этап 1. Каркас пакета.** `package.json`, четыре asmdef, README-заглушка. Проверка: проект открывается без ошибок.

**Этап 2. Модель графа + правила портов.** Классы из 2.3, все узлы из 2.2 (только данные),
`QuestPortRules`, ассет создаётся через меню Create. EditMode-тесты: создание графа, добавление и удаление
узлов и связей, правила портов, сохранение и перезагрузка ассета с `[SerializeReference]`-узлами.

**Этап 3a. Окно и библиотека.** Окно, верхняя панель, библиотека, рабочая копия, Save / Save As / New / Open,
диалог несохранённых изменений, `OnOpenAsset`, переживание domain reload. Граф пока заглушка.

**Этап 3b. Холст графа.** GraphView, сетка, зум, панорамирование, `SearchWindow`, универсальный `QuestNodeView`,
перемещение и удаление узлов, сохранение позиций.

**Этап 3c. Связи и Undo.** Создание и удаление связей, `GetCompatiblePorts`, отклонение недопустимых связей,
Undo/Redo с пересборкой графа. Проверка: после закрытия окна и перезапуска Unity граф восстанавливается полностью.

**Этап 4. Инспектор узла.** Универсальный инспектор на `PropertyField`, обновление заголовка, тултипы, настройки квеста.

**Этап 5. Валидация.** `QuestGraphValidator`, панель диагностики, переход к узлу, пометка невалидных квестов
в библиотеке. EditMode-тесты на каждое правило из раздела 4.

**Этап 6. Runtime.** `QuestEvent`, `QuestInstance`, `QuestGraphRunner`, `QuestManager`, `QuestRewardDispatcher`,
`QuestDatabase`, обработчики наград, события. Тесты: прогресс, последовательные цели, ветвление Condition,
Completed / Failed, AutoGrant / ManualClaim, отсутствующий обработчик, повторные события, отказ запускать невалидный граф.

**Этап 7. Сохранение.** DTO, `FileQuestSaveService`, Capture / Restore. Тесты: round-trip, отсутствующий файл,
битый JSON, неизвестный квест, отсутствие повторной выдачи наград после загрузки.

**Этап 8. Демо.** `Assets/QuestEditorDemo/`: сцена, bootstrap-MonoBehaviour, создающий `QuestManager`
и тестовые реализации `ICurrencyService` / `IExperienceService`, кнопка «Убить гоблина», UI прогресса,
золото и опыт, сообщение о завершении, Save/Load. Только публичный API. Затем — перенос в `Samples~/`.

**Этап 9. Документация и выпуск.** README на русском: назначение, версия Unity (2023.1.22f1), установка
(локально и по git URL `...?path=/Packages/com.vokazhira.quest-editor`), структура, первое задание,
узлы и связи, цели и награды, использование `QuestManager`, регистрация сервисов, сохранения,
свой тип цели / условия / награды (пример по ~20 строк), `[MovedFrom]`, ограничения, запуск тестов.

## 11. Критерии готовности MVP

1. Окно открывается через `Tools → Quest Editor` и по двойному клику на ассет.
2. Можно создать задание, добавить узлы, соединить совместимые порты; несовместимые — отклоняются.
3. Параметры узлов редактируются в инспекторе, Undo/Redo работает.
4. Граф сохраняется и полностью восстанавливается после перезапуска Unity.
5. Несохранённые изменения не теряются молча.
6. Валидатор находит все ошибки из раздела 4, можно перейти к узлу.
7. Runtime исполняет валидный граф без Editor API и не запускает невалидный.
8. События игры обновляют прогресс; ветвление работает; награды выдаются при завершении.
9. Прогресс сохраняется и восстанавливается; награды не выдаются повторно.
10. Новый тип цели или награды добавляется в пользовательском проекте без правок пакета.
11. Тесты основной логики проходят (фактически запущены).
12. README с примером; пакет устанавливается в чистый проект на 2023.1.22f1.

Пункты, которые не удалось проверить в Unity, не отмечай как выполненные — дай список ручных проверок.

## 12. Итоговый отчёт

Архитектура и схема компонентов, публичный API, установка, первое задание, результаты тестов
(что реально запускалось), известные ограничения, план развития (параллельные цели, новые условия,
debugger-окно в Play Mode, проверка квестов при сборке, экспорт и импорт JSON, локализация).
