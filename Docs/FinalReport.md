# Quest Editor — итоговый отчёт (MVP 0.1.0)

Отчёт по разделу 12 спецификации. Подробности: архитектура — `Docs/Architecture.md`, решения — `Docs/Decisions.md`
(D-001…D-080), руководство пользователя — `Packages/com.vokazhira.quest-editor/README.md` (англ.) и `README.ru.md`.

## 1. Архитектура и компоненты

```
Editor (Vokazhira.QuestEditor.Editor, только редактор)
  QuestEditorWindow ── QuestEditorToolbar, QuestSettingsPanel, QuestDiagnosticsView
        ├── QuestLibraryView  ◄── QuestLibraryPostprocessor (события ассетов → delayCall)
        ├── QuestGraphView : GraphView ── QuestNodeView (один на все типы), QuestNodeSearchProvider ◄── QuestNodeTypeCache (TypeCache)
        ├── QuestNodeInspector (PropertyField на nodes.Array.data[i])
        └── QuestWorkingCopy (Instantiate + DontSave, Save через CopySerialized, JSON-снимок для «*»)
  QuestGraphEditing (все правки модели через Undo), QuestEditorValidation, QuestAssetUtility, QuestDatabaseEditor
                                   │ ссылается
Runtime (Vokazhira.QuestEditor, без UnityEditor)
  Graph:       QuestGraphAsset, QuestNodeData, QuestEdgeData, QuestPortDefinition, QuestPortRules, [QuestNode]
  Nodes:       Start, End, Kill/Collect/CompleteWave, QuestCompletedCondition, Gold/Experience
  Validation:  QuestGraphValidator → QuestDiagnostic (QE001…QE020, QW001…QW004)
  Execution:   QuestManager ─┬─ QuestGraphRunner      (переходы по графу)
                             ├─ QuestRewardDispatcher (IRewardHandler<T>)
                             └─ QuestInstance         (состояние задания у игрока)
               QuestDatabase, QuestEvent
  Services:    ICurrencyService, IExperienceService, GoldRewardHandler, ExperienceRewardHandler
  Persistence: QuestSaveData, IQuestSaveService, FileQuestSaveService, QuestSaveMapper
                                   ▲ создаёт и вызывает
Игра / пример Samples~/BasicQuestExample (QuestDemoBootstrap, QuestDemoUI, QuestDemoAssetBuilder)
```

Принципы: модель — единственный источник правды (вид графа пересобирается из неё); редактор правит рабочую копию;
runtime исполняет тот же ассет и только после валидации; новые типы узлов добавляются без правки пакета; прогресс игрока
хранится отдельно от определений.

## 2. Публичный API

- **Модель:** `QuestGraphAsset` (`QuestId`, `DisplayName`, `Description`, `RewardMode`, `Nodes`, `Edges`, `FindNode`,
  `AddNode`, `RemoveNode`, `TryConnect`, `Disconnect`), `QuestNodeData` (`Id`, `Position`, `Ports`, `Validate`, `GetSummary`),
  базовые `ObjectiveNodeData`, `ConditionNodeData`, `RewardNodeData`, `[QuestNode]`, `QuestPortRules`.
- **Валидация:** `QuestGraphValidator.Validate(graph, otherQuests)`, `HasErrors`, `QuestDiagnostic`, `QuestDiagnosticCodes`,
  `QuestValidationContext`.
- **Исполнение:** `QuestManager(database, saveService, logger = null)` — `GetState`, `GetInstance`, `GetInstances`,
  `GetValidationErrors`, `CanStart`, `Start`, `Abandon`, `Report`, `Claim`, `RegisterRewardHandler<T>`, `Capture`,
  `Restore`, `Save`, `Load`, `Dispose`; события `QuestStarted`, `QuestProgressChanged`, `QuestClaimable`, `QuestCompleted`,
  `QuestFailed`, `QuestRewardGranted`, `QuestRewardFailed`. `QuestInstance`, `QuestState`, `QuestEvent`, `QuestDatabase`.
- **Сервисы и сохранения:** `IRewardHandler<T>`, `ICurrencyService`, `IExperienceService`, `IQuestSaveService`,
  `FileQuestSaveService`, `QuestSaveData`.

## 3. Установка и первое задание

Описаны в README (разделы Installation и Your first quest): Git URL
`https://github.com/vokazhira/QuestEditorDemonstration.git?path=/Packages/com.vokazhira.quest-editor` или локальная копия
в `Packages/`; первое задание собирается за 10 шагов в окне **Tools → Quest Editor**.

## 4. Результаты тестов (что реально запускалось)

- Тесты запускал **заказчик** в Unity 2023.1.22f1 через Test Runner (EditMode). Последний прогон — после перевода
  пользовательских текстов на английский и выпуска документации (итоговое состояние ветки): **172 из 172**.
- Сам я ни компиляцию, ни тесты не запускал: в среде разработки нет Unity, а установка .NET SDK для проверки типов
  была заблокирована правилами среды. Мои проверки — статические: отсутствие `UnityEditor` в Runtime, конструкций C# 10+,
  `//`-комментариев, `PlayerPrefs`/`FindObjectOfType`/`Resources.Load`, баланс скобок, корректность JSON.

| Набор | Тестов |
|---|---|
| Runtime: граф, порты, узлы | 40 |
| Runtime: валидатор (каждое правило раздела 4) | 35 |
| Runtime: QuestManager и выдача наград | 28 |
| Runtime: сохранения и FileQuestSaveService | 26 |
| Editor: сериализация, рабочая копия, утилиты, Undo, типы узлов, инспектор, валидация в редакторе | 43 |
| **Всего** | **172** |

## 5. Критерии готовности MVP (раздел 11)

Обозначения: ✅ подтверждено тестами в Unity или заказчиком; 🟡 реализовано, но в Unity не подтверждено; ❌ не сделано.

| # | Критерий | Статус |
|---|---|---|
| 1 | Окно открывается через Tools → Quest Editor и по двойному клику на ассет | 🟡 реализовано; ручная проверка явно не подтверждалась |
| 2 | Создание задания, узлов, связей; несовместимые связи отклоняются | ✅ правила портов и связей — тестами; 🟡 интерфейс холста — вручную |
| 3 | Параметры узлов в инспекторе, Undo/Redo | ✅ правка через SerializedProperty и Undo — тестами; 🟡 интерфейс — вручную |
| 4 | Граф сохраняется и полностью восстанавливается после перезапуска Unity | ✅ сохранение и перезагрузка ассета — тестами; 🟡 перезапуск Unity не подтверждён |
| 5 | Несохранённые изменения не теряются молча | 🟡 реализовано (диалоги, `hasUnsavedChanges`); рабочая копия — тестами |
| 6 | Валидатор находит все ошибки раздела 4, переход к узлу | ✅ правила — 35 тестами; 🟡 переход к узлу — вручную |
| 7 | Runtime исполняет валидный граф без Editor API и не запускает невалидный | ✅ тестами; в Runtime-сборке нет ссылок на UnityEditor |
| 8 | События обновляют прогресс, ветвление работает, награды выдаются при завершении | ✅ тестами и в демо (заказчик) |
| 9 | Прогресс сохраняется и восстанавливается; награды не выдаются повторно | ✅ тестами; сохранение в демо подтверждено заказчиком |
| 10 | Новый тип цели или награды добавляется без правок пакета | 🟡 архитектура это поддерживает (`TypeCache`, `[QuestNode]`, `IRewardHandler<T>`), тестовые узлы объявлены вне Runtime-сборки; в меню редактора не проверялось |
| 11 | Тесты основной логики проходят (фактически запущены) | ✅ 172/172 на итоговом состоянии (запуск заказчиком в Unity 2023.1.22f1) |
| 12 | README с примером; пакет ставится в чистый проект на 2023.1.22f1 | ✅ README (EN + RU); 🟡 установка в чистый проект не проверялась |

### Ручные проверки, которые остались

1. Окно: Tools → Quest Editor, двойной клик по ассету, New/Open/Save/Save As, диалог несохранённых изменений, перекомпиляция.
2. Холст: создание узла через Space и контекстное меню, связи, отказ при цикле, Delete + Ctrl+Z, защита единственного Start.
3. Инспектор: правка полей, обновление заголовка, End: Completed → Failed убирает связи наград.
4. Перезапуск Unity: граф восстанавливается полностью.
5. Свой тип узла в отдельной сборке проекта появляется в меню Create Node.
6. Установка пакета по Git URL в чистый проект на 2023.1.22f1 и импорт примера.

## 6. Известные ограничения

- Один текущий узел на задание, без параллельных веток; Flow-циклы запрещены.
- Условия видят только состояния заданий; данных игры и выражений в условиях нет.
- Повтор выполненных и проваленных заданий не поддерживается; `IsOptional` только хранится.
- Нет копирования узлов и мини-карты; ширина панелей окна фиксирована.
- Пример на IMGUI; золото и опыт в примере не сохраняются (это данные игры, а не заданий).
- Одна версия формата сохранения, миграций нет.
- Нет локализации.

## 7. План развития

Параллельные цели, новые условия (сравнение чисел, наличие предмета, состояние объекта) с доступом к данным игры,
предусловия запуска заданий, окно-отладчик в Play Mode, проверка заданий при сборке, экспорт и импорт JSON, локализация.
